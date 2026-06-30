using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using BCrypt.Net;
using System.Collections.Generic;
using System.Linq;

namespace GuidYu_API.Sql;

public class DatabaseInitializer
{
    private readonly IConfiguration _configuration;

    public DatabaseInitializer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Initialize()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection") 
                               ?? throw new InvalidOperationException("DefaultConnection string not found.");

        // First attempt: Connect directly to target database (needed for Azure SQL where master DB access is restricted)
        try
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                CreateTables(connection);
                return; // Initialization successful, skip fallback
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Direct database connection failed (normal if database does not exist yet): {ex.Message}");
            Console.WriteLine("Attempting fallback to 'master' database to check/create database...");
        }

        // Second attempt: Fallback to master to create database (standard for local dev)
        try 
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            
            builder.InitialCatalog = "master";
            var masterConnection = builder.ConnectionString;

            using (var connection = new SqlConnection(masterConnection))
            {
                var records = connection.Query(
                    $"SELECT name FROM sys.databases WHERE name = N'{databaseName}'"
                );

                if (!records.Any())
                {
                    connection.Execute($"CREATE DATABASE [{databaseName}]");
                }
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                CreateTables(connection);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("CRITICAL: Database Initialization Failed!");
            Console.WriteLine(ex.Message);
        }
    }

    private void CreateTables(IDbConnection connection)
    {
        connection.Execute(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
            BEGIN
                CREATE TABLE Users (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    FullName NVARCHAR(100) NOT NULL,
                    Email NVARCHAR(255) NOT NULL UNIQUE,
                    PasswordHash NVARCHAR(MAX) NOT NULL,
                    GoogleId NVARCHAR(255) NULL,
                    AppleId NVARCHAR(255) NULL,
                    AuthProvider NVARCHAR(50) DEFAULT 'Manual' NOT NULL,
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    IsEmailVerified BIT DEFAULT 0,
                    LastLoginAt DATETIME NULL,
                    RefreshToken NVARCHAR(255) NULL,
                    RefreshTokenExpiryTime DATETIME NULL,
                    Role NVARCHAR(50) DEFAULT 'User' NOT NULL,
                    ProfileImageUrl NVARCHAR(MAX) NULL,
                    IsBlocked BIT DEFAULT 0
                );
            END");

        var columns = new[] { 
            "ProfileImageUrl NVARCHAR(MAX) NULL",
            "IsBlocked BIT DEFAULT 0"
        };

        foreach (var col in columns)
        {
            var name = col.Split(' ')[0];
            connection.Execute($@"
                IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'{name}' AND Object_ID = Object_ID(N'Users'))
                BEGIN
                    ALTER TABLE Users ADD {col};
                END");
        }

        var columnsToDrop = new[] { "ResetPasswordToken", "TokenExpiry" };
        foreach (var col in columnsToDrop)
        {
            if (connection.ExecuteScalar<int>($@"SELECT COUNT(*) FROM sys.columns WHERE Name = N'{col}' AND Object_ID = Object_ID(N'Users')") > 0)
            {
                try {
                    connection.Execute($"ALTER TABLE Users DROP COLUMN {col};");
                } catch (Exception ex) {
                    Console.WriteLine($"Cleanup Warning (Drop {col}): " + ex.Message);
                }
            }
        }

        // Migrate and Consolidate User Details
        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.tables WHERE name IN ('UserAcademicDetails', 'UserProfessionalDetails', 'UserSkillDetails')") > 0)
        {
            try {
                // Ensure UserDetails exists first
                connection.Execute(@"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserDetails' AND xtype='U')
                    CREATE TABLE UserDetails (
                        UserId INT PRIMARY KEY,
                        HighestQualification NVARCHAR(50) NULL,
                        Stream NVARCHAR(50) NULL,
                        InstitutionName NVARCHAR(150) NULL,
                        CurrentStatus NVARCHAR(50) NULL,
                        CareerGoal NVARCHAR(100) NULL,
                        PreferredIndustry NVARCHAR(100) NULL,
                        Skills NVARCHAR(MAX) NULL,
                        SkillLevels NVARCHAR(MAX) NULL,
                        Interests NVARCHAR(MAX) NULL,
                        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                    );
                ");

                // Migrate from old tables if they exist
                connection.Execute(@"
                    -- Sync from Academic
                    IF EXISTS (SELECT * FROM sysobjects WHERE name='UserAcademicDetails' AND xtype='U')
                    BEGIN
                        INSERT INTO UserDetails (UserId, HighestQualification, Stream, InstitutionName)
                        SELECT UserId, HighestQualification, Stream, InstitutionName FROM UserAcademicDetails
                        WHERE UserId NOT IN (SELECT UserId FROM UserDetails);
                        
                        UPDATE ud
                        SET ud.HighestQualification = uad.HighestQualification, ud.Stream = uad.Stream, ud.InstitutionName = uad.InstitutionName
                        FROM UserDetails ud JOIN UserAcademicDetails uad ON ud.UserId = uad.UserId;
                    END

                    -- Sync from Professional
                    IF EXISTS (SELECT * FROM sysobjects WHERE name='UserProfessionalDetails' AND xtype='U')
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM UserDetails WHERE UserId IN (SELECT UserId FROM UserProfessionalDetails))
                        BEGIN
                            INSERT INTO UserDetails (UserId) SELECT UserId FROM UserProfessionalDetails WHERE UserId NOT IN (SELECT UserId FROM UserDetails);
                        END
                        
                        UPDATE ud
                        SET ud.CurrentStatus = upd.CurrentStatus, ud.CareerGoal = upd.CareerGoal, ud.PreferredIndustry = upd.PreferredIndustry
                        FROM UserDetails ud JOIN UserProfessionalDetails upd ON ud.UserId = upd.UserId;
                    END

                    -- Sync from Skills
                    IF EXISTS (SELECT * FROM sysobjects WHERE name='UserSkillDetails' AND xtype='U')
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM UserDetails WHERE UserId IN (SELECT UserId FROM UserSkillDetails))
                        BEGIN
                            INSERT INTO UserDetails (UserId) SELECT UserId FROM UserSkillDetails WHERE UserId NOT IN (SELECT UserId FROM UserDetails);
                        END
                        
                        UPDATE ud
                        SET ud.Skills = usd.Skills, ud.SkillLevels = usd.SkillLevels, ud.Interests = usd.Interests
                        FROM UserDetails ud JOIN UserSkillDetails usd ON ud.UserId = usd.UserId;
                    END

                    -- Drop old tables
                    IF EXISTS (SELECT * FROM sysobjects WHERE name='UserAcademicDetails' AND xtype='U') DROP TABLE UserAcademicDetails;
                    IF EXISTS (SELECT * FROM sysobjects WHERE name='UserProfessionalDetails' AND xtype='U') DROP TABLE UserProfessionalDetails;
                    IF EXISTS (SELECT * FROM sysobjects WHERE name='UserSkillDetails' AND xtype='U') DROP TABLE UserSkillDetails;
                ");
            } catch (Exception ex) {
                Console.WriteLine("Consolidation Warning: " + ex.Message);
            }
        }

        // Final Ensure UserDetails exists (for fresh installs)
        connection.Execute(@"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserDetails' AND xtype='U')
            CREATE TABLE UserDetails (
                UserId INT PRIMARY KEY,
                HighestQualification NVARCHAR(50) NULL,
                Stream NVARCHAR(50) NULL,
                InstitutionName NVARCHAR(150) NULL,
                CurrentStatus NVARCHAR(50) NULL,
                CareerGoal NVARCHAR(100) NULL,
                PreferredIndustry NVARCHAR(100) NULL,
                Skills NVARCHAR(MAX) NULL,
                SkillLevels NVARCHAR(MAX) NULL,
                Interests NVARCHAR(MAX) NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );
        ");

        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.columns WHERE Name = N'HighestQualification' AND Object_ID = Object_ID(N'Users')") > 0)
        {
            try {
                connection.Execute(@"
                    INSERT INTO UserDetails (UserId, HighestQualification, Stream, InstitutionName, CurrentStatus, CareerGoal, PreferredIndustry, Skills, SkillLevels, Interests)
                    SELECT Id, HighestQualification, Stream, InstitutionName, CurrentStatus, CareerGoal, PreferredIndustry, Skills, SkillLevels, Interests 
                    FROM Users WHERE Id NOT IN (SELECT UserId FROM UserDetails);

                    -- CLEANUP: Drop old columns from Users
                    ALTER TABLE Users DROP COLUMN HighestQualification, Stream, InstitutionName, CurrentStatus, CareerGoal, PreferredIndustry, Skills, SkillLevels, Interests;
                ");
            } catch (Exception ex) {
                Console.WriteLine("Migration Warning (Users to UserDetails): " + ex.Message);
            }
        }

        connection.Execute(@"
            -- Consolidated Dashboard Table
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DashboardData' AND xtype='U')
                CREATE TABLE DashboardData (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    UserId INT NOT NULL,
                    Category NVARCHAR(50) NOT NULL, -- Metric, Progress, NextStep, Insight, Roadmap, Activity
                    JsonData NVARCHAR(MAX) NOT NULL,
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );

            -- Admin Tables
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Careers' AND xtype='U')
            CREATE TABLE Careers (
                Id INT PRIMARY KEY IDENTITY(1,1),
                Title NVARCHAR(200) NOT NULL,
                Description NVARCHAR(MAX) NULL,
                Difficulty NVARCHAR(50) NULL,
                Category NVARCHAR(100) NULL,
                ThumbnailUrl NVARCHAR(MAX) NULL,
                CreatedAt DATETIME DEFAULT GETDATE()
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Roadmaps' AND xtype='U')
            CREATE TABLE Roadmaps (
                Id INT PRIMARY KEY IDENTITY(1,1),
                CareerId INT NOT NULL,
                Title NVARCHAR(200) NOT NULL,
                Description NVARCHAR(MAX) NULL,
                CreatedAt DATETIME DEFAULT GETDATE(),
                FOREIGN KEY (CareerId) REFERENCES Careers(Id) ON DELETE CASCADE
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RoadmapModules' AND xtype='U')
            CREATE TABLE RoadmapModules (
                Id INT PRIMARY KEY IDENTITY(1,1),
                RoadmapId INT NOT NULL,
                Title NVARCHAR(200) NOT NULL,
                [Order] INT NOT NULL DEFAULT 0,
                FOREIGN KEY (RoadmapId) REFERENCES Roadmaps(Id) ON DELETE CASCADE
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ModuleTopics' AND xtype='U')
            CREATE TABLE ModuleTopics (
                Id INT PRIMARY KEY IDENTITY(1,1),
                ModuleId INT NOT NULL,
                Title NVARCHAR(200) NOT NULL,
                Difficulty NVARCHAR(50) DEFAULT 'Intermediate' NOT NULL,
                [Order] INT NOT NULL DEFAULT 0,
                FOREIGN KEY (ModuleId) REFERENCES RoadmapModules(Id) ON DELETE CASCADE
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TopicProgress' AND xtype='U')
            CREATE TABLE TopicProgress (
                Id INT PRIMARY KEY IDENTITY(1,1),
                UserId INT NOT NULL,
                TopicId INT NOT NULL,
                IsCompleted BIT DEFAULT 0,
                LastAccessed DATETIME DEFAULT GETDATE(),
                UNIQUE(UserId, TopicId)
            );


            -- Resume Table
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Resume' AND xtype='U')
            CREATE TABLE Resume (
                Id INT PRIMARY KEY IDENTITY(1,1),
                UserId INT NOT NULL,
                CareerId INT NULL,
                FullName NVARCHAR(100) NOT NULL,
                Email NVARCHAR(255) NOT NULL,
                PhoneNumber NVARCHAR(20) NULL,
                Location NVARCHAR(200) NULL,
                ProfessionalSummary NVARCHAR(MAX) NULL,
                Skills NVARCHAR(MAX) NULL,
                Education NVARCHAR(MAX) NULL,
                Projects NVARCHAR(MAX) NULL,
                Experience NVARCHAR(MAX) NULL,
                Certifications NVARCHAR(MAX) NULL,
                GithubUrl NVARCHAR(MAX) NULL,
                LinkedInUrl NVARCHAR(MAX) NULL,
                PortfolioUrl NVARCHAR(MAX) NULL,
                CreatedAt DATETIME DEFAULT GETDATE(),
                UpdatedAt DATETIME DEFAULT GETDATE(),
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );
            
            -- MyCareerMap Table
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MyCareerMap' AND xtype='U')
            BEGIN
                CREATE TABLE MyCareerMap (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    UserId INT NOT NULL,
                    CareerName NVARCHAR(200) NOT NULL,
                    ModuleName NVARCHAR(200) NOT NULL,
                    SyllabusData NVARCHAR(MAX) NOT NULL,
                    LessonsData NVARCHAR(MAX) NULL,
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );
            END

            -- SavedLessons Table
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SavedLessons' AND xtype='U')
            BEGIN
                CREATE TABLE SavedLessons (
                    Id INT PRIMARY KEY IDENTITY(1,1),
                    UserId INT NOT NULL,
                    CareerName NVARCHAR(200) NOT NULL,
                    ModuleName NVARCHAR(200) NOT NULL,
                    ChapterName NVARCHAR(200) NOT NULL,
                    LessonId NVARCHAR(200) NOT NULL,
                    GeneratedContent NVARCHAR(MAX) NOT NULL,
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                    CONSTRAINT UQ_SavedLessons UNIQUE(UserId, CareerName, ModuleName, LessonId)
                );
            END

            -- SavedQuizzes Table: stores full quiz WITH correct answers server-side only
            -- If the table exists but contains the legacy GeneratedQuiz column, drop it so it gets recreated.
            IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'GeneratedQuiz' AND Object_ID = Object_ID(N'SavedQuizzes'))
            BEGIN
                DROP TABLE SavedQuizzes;
            END

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SavedQuizzes' AND xtype='U')
            BEGIN
                CREATE TABLE SavedQuizzes (
                    Id        INT PRIMARY KEY IDENTITY(1,1),
                    UserId    INT NOT NULL,
                    LessonId  NVARCHAR(300) NOT NULL,
                    QuizJson  NVARCHAR(MAX) NOT NULL,
                    CreatedAt DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                    CONSTRAINT UQ_SavedQuizzes UNIQUE(UserId, LessonId)
                );
            END

            -- LessonQuizResults Table: attempt history, analytics, retry tracking
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LessonQuizResults' AND xtype='U')
            BEGIN
                CREATE TABLE LessonQuizResults (
                    Id           INT PRIMARY KEY IDENTITY(1,1),
                    UserId       INT NOT NULL,
                    LessonId     NVARCHAR(300) NOT NULL,
                    Score        INT NOT NULL,
                    Total        INT NOT NULL,
                    Passed       BIT NOT NULL DEFAULT 0,
                    AttemptCount INT NOT NULL DEFAULT 1,
                    CompletedAt  DATETIME DEFAULT GETDATE(),
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );
            END
            ");

            // Ensure Difficulty column exists in ModuleTopics if table already existed
            connection.Execute(@"
                IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'Difficulty' AND Object_ID = Object_ID(N'ModuleTopics'))
                BEGIN
                    ALTER TABLE ModuleTopics ADD Difficulty NVARCHAR(50) DEFAULT 'Intermediate' NOT NULL;
                END");

            // Cleanup Old Dashboard & Skill Training Tables
            // NOTE: "Certificates" is included here to drop it if it was ever created in a prior session.
            // The certification feature has been removed from this project.
            var tablesToDrop = new[] 
            { 
                "UserSkillTrainingProgress", "SkillTrainingActivity", "SkillTrainingPerformance", "SkillTrainingModule",
                "DashboardMetrics", "CareerVelocity", "CareerTargets", "AIInsights", "LearningRoadmap", "ActivityFeed",
                "AiPromptTemplates", "Certificates"
            };
            foreach (var table in tablesToDrop)
            {
                connection.Execute($@"IF EXISTS (SELECT * FROM sysobjects WHERE name='{table}' AND xtype='U') DROP TABLE {table}");
            }

            SeedDashboardData(connection);
            SeedAdminData(connection);
    }

    private void SeedAdminData(IDbConnection connection)
    {
        // Seed/Update Default Admin
        var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
        connection.Execute(@"
            IF EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@gmail.com')
            BEGIN
                UPDATE Users SET FullName = 'Admin', Role = 'Admin' WHERE Email = 'admin@gmail.com'
            END
            ELSE
            BEGIN
                INSERT INTO Users (FullName, Email, PasswordHash, Role, CreatedAt, AuthProvider, IsEmailVerified)
                VALUES ('Admin', 'admin@gmail.com', @PasswordHash, 'Admin', GETDATE(), 'Manual', 1)
            END", 
            new { PasswordHash = adminPasswordHash });

        // Guaranteed seed for trending careers to ensure sections are populated
        var trendingCareers = new[] {
            new { Title = "Cloud Solutions Architect", Desc = "Design and manage scalable cloud infrastructure on AWS, Azure, and GCP.", Diff = "Advanced", Cat = "Technology" },
            new { Title = "Full Stack Developer", Desc = "Master both frontend and backend technologies to build complete web applications.", Diff = "Intermediate", Cat = "Technology" },
            new { Title = "Cybersecurity Analyst", Desc = "Protect organizational assets by implementing advanced neural security protocols.", Diff = "Advanced", Cat = "Technology" },
            new { Title = "UI/UX Designer", Desc = "Create intuitive and beautiful user interfaces with a focus on user experience.", Diff = "Beginner", Cat = "Design" },
            new { Title = "Motion Graphics Designer", Desc = "Architect dynamic visual narratives and fluid interface transitions.", Diff = "Intermediate", Cat = "Design" },
            new { Title = "Brand Identity Lead", Desc = "Develop cohesive visual systems and high-fidelity brand architectures.", Diff = "Intermediate", Cat = "Design" },
            new { Title = "Digital Marketing Strategist", Desc = "Execute data-driven marketing campaigns and optimize conversion trajectories.", Diff = "Intermediate", Cat = "Business" },
            new { Title = "Product Manager", Desc = "Orchestrate product lifecycles and align neural user feedback with business goals.", Diff = "Advanced", Cat = "Business" },
            new { Title = "Growth Operations Lead", Desc = "Scale organizational ecosystems through data-driven strategic optimization.", Diff = "Advanced", Cat = "Business" }
        };

        foreach (var c in trendingCareers)
        {
            connection.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM Careers WHERE Title = @Title)
                BEGIN
                    INSERT INTO Careers (Title, Description, Difficulty, Category)
                    VALUES (@Title, @Desc, @Diff, @Cat)
                END", c);
        }

        // Seed Roadmaps, Modules, and Topics for each Career
        SeedCareerRoadmaps(connection);
    }

    private void SeedCareerRoadmaps(IDbConnection connection)
    {
        var careers = connection.Query<dynamic>("SELECT Id, Title FROM Careers").ToList();

        foreach (var career in careers)
        {
            int careerId = (int)career.Id;
            string title = (string)career.Title;

            // Check if Roadmap exists
            var roadmapId = connection.ExecuteScalar<int?>(
                "SELECT Id FROM Roadmaps WHERE CareerId = @CareerId", new { CareerId = careerId });

            if (roadmapId == null)
            {
                roadmapId = connection.ExecuteScalar<int>(@"
                    INSERT INTO Roadmaps (CareerId, Title, Description)
                    VALUES (@CareerId, @Title, @Desc);
                    SELECT SCOPE_IDENTITY();", 
                    new { 
                        CareerId = careerId, 
                        Title = $"{title} Mastery Path", 
                        Desc = $"The ultimate, complete dynamic roadmap to master all skills needed for {title} roles." 
                    });
            }

            // Seed Modules & Topics based on career title
            SeedModulesForCareer(connection, (int)roadmapId, title);
        }
    }

    private void SeedModulesForCareer(IDbConnection connection, int roadmapId, string careerTitle)
    {
        // Define modules and topics dynamically
        var modules = new List<(string ModuleTitle, int Order, List<(string TopicTitle, string Difficulty, int Order)> Topics)>();

        if (careerTitle == "Full Stack Developer")
        {
            modules.Add(("Frontend Core Architecture", 1, new List<(string, string, int)> {
                ("Responsive Design & CSS Layouts", "Beginner", 1),
                ("React Components, Hooks & State", "Intermediate", 2),
                ("State Management & Context API", "Intermediate", 3)
            }));
            modules.Add(("Backend Services & Databases", 2, new List<(string, string, int)> {
                ("Node.js & Express RESTful APIs", "Intermediate", 1),
                ("Relational & NoSQL Database Integration", "Intermediate", 2),
                ("ASP.NET Core Web API Development", "Advanced", 3)
            }));
            modules.Add(("Deployment & DevOps", 3, new List<(string, string, int)> {
                ("Docker Containers & Orchestration", "Intermediate", 1),
                ("CI/CD Pipeline Automation", "Advanced", 2),
                ("Cloud Hosting & Serverless Architecture", "Advanced", 3)
            }));
        }
        else if (careerTitle == "Cloud Solutions Architect")
        {
            modules.Add(("Cloud Computing Fundamentals", 1, new List<(string, string, int)> {
                ("Introduction to Cloud Models (IaaS, PaaS, SaaS)", "Beginner", 1),
                ("Virtual Networks & Subnets", "Intermediate", 2),
                ("Compute Instances & Scaling", "Intermediate", 3)
            }));
            modules.Add(("Security & IAM", 2, new List<(string, string, int)> {
                ("Identity & Access Management (IAM)", "Intermediate", 1),
                ("Cloud Firewalls & Network Security Groups", "Intermediate", 2),
                ("Data Encryption At Rest & In Transit", "Advanced", 3)
            }));
            modules.Add(("Architecting for High Availability", 3, new List<(string, string, int)> {
                ("Load Balancers & Traffic Managers", "Intermediate", 1),
                ("Multi-Region Deployments & DR Strategies", "Advanced", 2),
                ("Infrastructure as Code (Terraform/CloudFormation)", "Advanced", 3)
            }));
        }
        else if (careerTitle == "Cybersecurity Analyst")
        {
            modules.Add(("Network Security & Cryptography", 1, new List<(string, string, int)> {
                ("Symmetric & Asymmetric Encryption", "Beginner", 1),
                ("Firewall Systems & Intrusion Detection", "Intermediate", 2),
                ("Secure Shell (SSH) & Transport Security", "Intermediate", 3)
            }));
            modules.Add(("Threat Intelligence & Assessment", 2, new List<(string, string, int)> {
                ("Common Vulnerabilities & Exploits (CVE)", "Intermediate", 1),
                ("Vulnerability Scanning & Penetration Testing", "Advanced", 2),
                ("Malware Analysis & Forensics", "Advanced", 3)
            }));
            modules.Add(("Security Compliance & Auditing", 3, new List<(string, string, int)> {
                ("ISO 27001 & SOC 2 Security Frameworks", "Intermediate", 1),
                ("Access Control Models & Auditing", "Intermediate", 2),
                ("Incident Response & Disaster Recovery Planning", "Advanced", 3)
            }));
        }
        else if (careerTitle == "UI/UX Designer")
        {
            modules.Add(("User Research & Interaction Principles", 1, new List<(string, string, int)> {
                ("Conducting Effective User Interviews", "Beginner", 1),
                ("Creating User Personas & Journey Maps", "Intermediate", 2),
                ("Heuristic Evaluation & Usability Testing", "Intermediate", 3)
            }));
            modules.Add(("Visual Design & UI Frameworks", 2, new List<(string, string, int)> {
                ("Color Theory, Typography & Grid Systems", "Beginner", 1),
                ("Building Modular Design Systems in Figma", "Intermediate", 2),
                ("Designing for Accessibility (WCAG Compliance)", "Advanced", 3)
            }));
            modules.Add(("Prototyping & Dynamic Animation", 3, new List<(string, string, int)> {
                ("Creating High-Fidelity Interactive Prototypes", "Intermediate", 1),
                ("Micro-interactions & UX Motion Principles", "Intermediate", 2),
                ("Developer Handoff & Spec Documentation", "Advanced", 3)
            }));
        }
        else if (careerTitle == "Motion Graphics Designer")
        {
            modules.Add(("Principles of Motion Design", 1, new List<(string, string, int)> {
                ("The 12 Principles of Animation in Motion Graphics", "Beginner", 1),
                ("Timing, Easing & Keyframe Interpolation", "Intermediate", 2),
                ("Storyboard Construction & Visual Rhythm", "Intermediate", 3)
            }));
            modules.Add(("Software & Asset Creation", 2, new List<(string, string, int)> {
                ("Vector Art Preparation & Rigging", "Intermediate", 1),
                ("After Effects Expression Writing & Automation", "Advanced", 2),
                ("3D Asset Modeling & Texturing for Motion", "Advanced", 3)
            }));
            modules.Add(("Advanced Visual Effects & Compositing", 3, new List<(string, string, int)> {
                ("Color Grading, Light Dynamics & Shading", "Intermediate", 1),
                ("Particle Systems & Physics Simulations", "Advanced", 2),
                ("Audio Integration & Sound Design Alignment", "Intermediate", 3)
            }));
        }
        else if (careerTitle == "Brand Identity Lead")
        {
            modules.Add(("Brand Strategy & Core Identity", 1, new List<(string, string, int)> {
                ("Defining Brand Positioning & Value Proposition", "Beginner", 1),
                ("Visual Identity Architecture & Moodboards", "Intermediate", 2),
                ("Logo Design Principles & Semiotics", "Intermediate", 3)
            }));
            modules.Add(("Collateral & Touchpoints", 2, new List<(string, string, int)> {
                ("Designing Responsive Digital Brand Assets", "Intermediate", 1),
                ("Print Collateral, Packaging & Materials", "Intermediate", 2),
                ("Brand Experience Design & Environmental Graphics", "Advanced", 3)
            }));
            modules.Add(("Governance & Brand Systems", 3, new List<(string, string, int)> {
                ("Creating Comprehensive Brand Style Guides", "Intermediate", 1),
                ("Scale-invariant Asset Management Systems", "Intermediate", 2),
                ("Brand Launch Strategies & Stakeholder Alignment", "Advanced", 3)
            }));
        }
        else if (careerTitle == "Digital Marketing Strategist")
        {
            modules.Add(("Search Engine Optimization (SEO)", 1, new List<(string, string, int)> {
                ("On-Page Optimization & Keyword Architecture", "Beginner", 1),
                ("Technical SEO, Crawlability & Site Audits", "Intermediate", 2),
                ("Link Building & Backlink Profile Authority", "Intermediate", 3)
            }));
            modules.Add(("Paid Acquisition & Analytics", 2, new List<(string, string, int)> {
                ("Google Ads Campaign Architecture & Optimization", "Intermediate", 1),
                ("Social Media Paid Funnels (Meta, LinkedIn, TikTok)", "Intermediate", 2),
                ("Conversion Rate Optimization (CRO) & A/B Testing", "Advanced", 3)
            }));
            modules.Add(("Content & Retention Strategy", 3, new List<(string, string, int)> {
                ("Content Marketing & Editorial Calendar Planning", "Intermediate", 1),
                ("Email Automation, Lead Nurturing & CRM Setup", "Intermediate", 2),
                ("Advanced Analytics Dashboarding (GA4 & Looker Studio)", "Advanced", 3)
            }));
        }
        else if (careerTitle == "Product Manager")
        {
            modules.Add(("Product Discovery & Strategy", 1, new List<(string, string, int)> {
                ("Identifying Market Gaps & Customer Pain Points", "Beginner", 1),
                ("Defining Product Vision, MVP Scope & Strategy", "Intermediate", 2),
                ("Competitive Analysis & Market Positioning", "Intermediate", 3)
            }));
            modules.Add(("Execution & Agile Methodologies", 2, new List<(string, string, int)> {
                ("Writing Effective User Stories & PRDs", "Intermediate", 1),
                ("Agile Scrum Ceremonies & Prioritization Frameworks", "Intermediate", 2),
                ("Managing Cross-functional Engineering Dependencies", "Advanced", 3)
            }));
            modules.Add(("Growth & Analytics Optimization", 3, new List<(string, string, int)> {
                ("Defining Product KPIs & Success Metrics", "Intermediate", 1),
                ("A/B Testing & Funnel Retention Diagnostics", "Advanced", 2),
                ("Product-Led Growth (PLG) Strategy & Monetization", "Advanced", 3)
            }));
        }
        else if (careerTitle == "Growth Operations Lead")
        {
            modules.Add(("Funnel Diagnostics & Strategy", 1, new List<(string, string, int)> {
                ("Mapping the Pirate Funnel (AARRR Metrics)", "Beginner", 1),
                ("Data Stack Architecture & Event Tracking", "Intermediate", 2),
                ("Cohort Analysis & Customer Lifetime Value (LTV)", "Advanced", 3)
            }));
            modules.Add(("Growth Channel Optimization", 2, new List<(string, string, int)> {
                ("Organic Referral Funnels & Viral Loops", "Intermediate", 1),
                ("Scalable Landing Page & Conversion Optimization", "Intermediate", 2),
                ("Programmatic Paid Acquisition Models", "Advanced", 3)
            }));
            modules.Add(("Growth Engine Automation", 3, new List<(string, string, int)> {
                ("Building Automated Growth Experiments", "Intermediate", 1),
                ("Integrating Marketing, Sales, & Product Tech Stacks", "Advanced", 2),
                ("Designing High-Fidelity Performance Dashboards", "Advanced", 3)
            }));
        }
        else
        {
            // No static fallback; rely on AI-generated modules only.
            // If needed, handle empty modules downstream.
        }

        foreach (var mod in modules)
        {
            // Check if Module exists under this roadmap
            var moduleId = connection.ExecuteScalar<int?>(
                "SELECT Id FROM RoadmapModules WHERE RoadmapId = @RoadmapId AND Title = @Title",
                new { RoadmapId = roadmapId, Title = mod.ModuleTitle });

            if (moduleId == null)
            {
                moduleId = connection.ExecuteScalar<int>(@"
                    INSERT INTO RoadmapModules (RoadmapId, Title, [Order])
                    VALUES (@RoadmapId, @Title, @Order);
                    SELECT SCOPE_IDENTITY();",
                    new { RoadmapId = roadmapId, Title = mod.ModuleTitle, Order = mod.Order });
            }

            foreach (var topic in mod.Topics)
            {
                // Check if Topic exists under this module
                var topicExists = connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM ModuleTopics WHERE ModuleId = @ModuleId AND Title = @Title",
                    new { ModuleId = moduleId, Title = topic.TopicTitle }) > 0;

                if (!topicExists)
                {
                    connection.Execute(@"
                        INSERT INTO ModuleTopics (ModuleId, Title, Difficulty, [Order])
                        VALUES (@ModuleId, @Title, @Difficulty, @Order)",
                        new { 
                            ModuleId = moduleId, 
                            Title = topic.TopicTitle, 
                            Difficulty = topic.Difficulty, 
                            Order = topic.Order 
                        });
                }
            }
        }
    }

    private void SeedDashboardData(IDbConnection connection)
    {
        var users = connection.Query<int>("SELECT Id FROM Users");
        foreach (var userId in users)
        {
            var hasData = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM DashboardData WHERE UserId = @UserId", new { UserId = userId }) > 0;
            if (!hasData)
            {
                // Seed Metrics
                var metrics = new[] {
                    new { Title = "Career Level", Value = "L4", Change = "+12%", Icon = "TrendingUp", PreviousValue = "L3", TrendDirection = "up", Timeframe = "this month", ActionAdvice = "Keep growing", ProgressPercentage = 40 },
                    new { Title = "Skill Index", Value = "84", Change = "+5.2%", Icon = "Target", PreviousValue = "80", TrendDirection = "up", Timeframe = "last 30 days", ActionAdvice = "Add cloud skills", ProgressPercentage = 84 },
                    new { Title = "Market Value", Value = "$142k", Change = "+8.4%", Icon = "Award", PreviousValue = "$130k", TrendDirection = "up", Timeframe = "yearly", ActionAdvice = "High demand area", ProgressPercentage = 90 },
                    new { Title = "Path Completion", Value = "72%", Change = "+2.1%", Icon = "Zap", PreviousValue = "70", TrendDirection = "up", Timeframe = "this week", ActionAdvice = "Almost there", ProgressPercentage = 72 }
                };
                foreach(var m in metrics) connection.Execute("INSERT INTO DashboardData (UserId, Category, JsonData) VALUES (@UserId, 'Metric', @Json)", new { UserId = userId, Json = JsonSerializer.Serialize(m) });

                // Seed Progress (Velocity)
                var velocity = new[] {
                    new { Month = "Jan", Velocity = 45 }, new { Month = "Feb", Velocity = 52 }, new { Month = "Mar", Velocity = 48 }, 
                    new { Month = "Apr", Velocity = 61 }, new { Month = "May", Velocity = 55 }, new { Month = "Jun", Velocity = 68 },
                    new { Month = "Jul", Velocity = 72 }, new { Month = "Aug", Velocity = 65 }, new { Month = "Sep", Velocity = 84 },
                    new { Month = "Oct", Velocity = 78 }, new { Month = "Nov", Velocity = 85 }, new { Month = "Dec", Velocity = 92 }
                };
                foreach(var v in velocity) connection.Execute("INSERT INTO DashboardData (UserId, Category, JsonData) VALUES (@UserId, 'Progress', @Json)", new { UserId = userId, Json = JsonSerializer.Serialize(v) });

                // Seed NextSteps (Targets)
                var targets = new[] {
                    new { Title = "Cloud Architect", Score = 94, MissingSkills = new[] { "Terraform", "Advanced Security" }, Icon = "Cloud", IsPrimary = true },
                    new { Title = "Senior DevRel", Score = 88, MissingSkills = new[] { "Public Speaking", "Strategy" }, Icon = "Globe", IsPrimary = false },
                    new { Title = "ML Operations Lead", Score = 76, MissingSkills = new[] { "MLOps", "Model Serving" }, Icon = "Cpu", IsPrimary = false }
                };
                foreach(var t in targets) connection.Execute("INSERT INTO DashboardData (UserId, Category, JsonData) VALUES (@UserId, 'NextStep', @Json)", new { UserId = userId, Json = JsonSerializer.Serialize(t) });

                // Seed Insights
                var insights = new[] {
                    new { Text = "Learning Docker can increase your market value by 18%", Highlight = "18%" },
                    new { Text = "You are close to qualifying for Senior DevRel roles", Highlight = "Senior DevRel" }
                };
                foreach(var i in insights) connection.Execute("INSERT INTO DashboardData (UserId, Category, JsonData) VALUES (@UserId, 'Insight', @Json)", new { UserId = userId, Json = JsonSerializer.Serialize(i) });

                // Seed Roadmap
                var roadmap = new[] {
                    new { Title = "Advanced Azure Architecting", Progress = 65, TimeRemaining = "2.5h remaining", Status = "In Progress" },
                    new { Title = "Security Best Practices in CI/CD", Progress = 30, TimeRemaining = "5h remaining", Status = "Next Up" }
                };
                foreach(var r in roadmap) connection.Execute("INSERT INTO DashboardData (UserId, Category, JsonData) VALUES (@UserId, 'Roadmap', @Json)", new { UserId = userId, Json = JsonSerializer.Serialize(r) });

                // Seed Activity
                var activity = new[] {
                    new { Type = "CheckCircle2", Title = "Certified", Content = "Completed Kubernetes Fundamentals", TimeAgo = "2 days ago" }
                };
                foreach(var a in activity) connection.Execute("INSERT INTO DashboardData (UserId, Category, JsonData) VALUES (@UserId, 'Activity', @Json)", new { UserId = userId, Json = JsonSerializer.Serialize(a) });
            }
        }
    }
}
