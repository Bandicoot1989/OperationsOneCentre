using Microsoft.Extensions.VectorData;

namespace VectorDataAI;

/// <summary>
/// Represents a recipe with all its details.
/// This class stores recipe information and its vector embedding for semantic search.
/// </summary>
public class Recipe
{
    /// <summary>
    /// Unique identifier for each recipe (0, 1, 2, etc.)
    /// The [VectorStoreKey] attribute tells the system this is the primary key
    /// </summary>
    [VectorStoreKey]
    public int Key { get; set; }

    /// <summary>
    /// Name of the recipe (e.g., "Spaghetti Carbonara")
    /// [VectorStoreData] means this field will be stored but not used for search
    /// </summary>
    [VectorStoreData]
    public required string Name { get; set; }

    /// <summary>
    /// Full description of what the recipe is and how it tastes
    /// This will be converted to a vector for semantic search
    /// </summary>
    [VectorStoreData]
    public required string Description { get; set; }

    /// <summary>
    /// Main ingredients needed (e.g., "chicken, rice, garlic, olive oil")
    /// </summary>
    [VectorStoreData]
    public required string Ingredients { get; set; }

    /// <summary>
    /// How long it takes to cook (e.g., "30 minutes")
    /// </summary>
    [VectorStoreData]
    public required string CookingTime { get; set; }

    /// <summary>
    /// How hard is it to make (Easy, Medium, Hard)
    /// </summary>
    [VectorStoreData]
    public required string Difficulty { get; set; }

    /// <summary>
    /// The vector embedding - a list of 384 numbers representing the meaning of this recipe
    /// This is what makes semantic search possible!
    /// Dimensions: 384 means our vector has 384 numbers
    /// DistanceFunction: CosineSimilarity is how we measure if two recipes are similar
    /// </summary>
    [VectorStoreVector(
        Dimensions: 384,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }
}
