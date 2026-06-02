using System.Collections.Generic;

namespace GuidYu_API.Models;

/// <summary>
/// Root response type for study material API calls.
/// </summary>
public class StudyMaterialResponse
{
    /// <summary>
    /// List of categories containing study material items.
    /// </summary>
    public List<StudyMaterialCategory> Categories { get; set; } = new();
}

/// <summary>
/// Represents a category of study materials (e.g., "Fundamentals", "Advanced Topics").
/// </summary>
public class StudyMaterialCategory
{
    /// <summary>
    /// Name of the category.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Collection of items within this category.
    /// </summary>
    public List<StudyMaterialItem> Items { get; set; } = new();
}

/// <summary>
/// Individual study material entry.
/// </summary>
public class StudyMaterialItem
{
    /// <summary>
    /// Title of the material (e.g., "React Basics").
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Platform providing the material (e.g., "YouTube", "Coursera").
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Short description of the material.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Direct link to the material.
    /// </summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// Approximate duration (e.g., "30 mins").
    /// </summary>
    public string EstimatedTime { get; set; } = string.Empty;
}
