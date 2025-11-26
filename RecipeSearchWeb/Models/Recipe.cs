namespace RecipeSearchWeb.Models;

/// <summary>
/// Represents a recipe with all its details for our cooking assistant
/// </summary>
public class Recipe
{
    public int Key { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Ingredients { get; set; }
    public required string CookingTime { get; set; }
    public required string Difficulty { get; set; }
    public required string Category { get; set; }  // New: Breakfast, Lunch, Dinner, Dessert, etc.
    public string Instructions { get; set; } = string.Empty;  // New: Step-by-step instructions
    public ReadOnlyMemory<float> Vector { get; set; }
    
    // Helper property for displaying emoji based on category
    public string CategoryEmoji => Category switch
    {
        "Breakfast" => "🌅",
        "Lunch" => "🌞",
        "Dinner" => "🌙",
        "Dessert" => "🍰",
        "Appetizer" => "🥗",
        "Snack" => "🍪",
        _ => "🍽️"
    };
}
