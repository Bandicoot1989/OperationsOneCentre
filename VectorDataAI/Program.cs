using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using VectorDataAI;

// Load the configuration values.
IConfigurationRoot config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
string endpoint = config["AZURE_OPENAI_ENDPOINT"] ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set");
string model = config["AZURE_OPENAI_GPT_NAME"] ?? throw new InvalidOperationException("AZURE_OPENAI_GPT_NAME is not set");
string apiKey = config["AZURE_OPENAI_API_KEY"] ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY is not set");

// Create the Azure OpenAI client
var azureClient = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));
var embeddingClient = azureClient.GetEmbeddingClient(model);

// Define the list of recipes
// Each recipe has: Key (ID), Name, Description, Ingredients, CookingTime, and Difficulty
List<Recipe> recipes =
[
    new() {
            Key = 0,
            Name = "Classic Spaghetti Carbonara",
            Description = "A creamy Italian pasta dish with eggs, cheese, pancetta, and black pepper. Rich, comforting, and authentic Roman cuisine. Perfect for a quick but impressive dinner.",
            Ingredients = "spaghetti, eggs, pancetta, Parmesan cheese, black pepper, salt",
            CookingTime = "20 minutes",
            Difficulty = "Medium"
    },
    new() {
            Key = 1,
            Name = "Grilled Chicken Tacos",
            Description = "Spicy and flavorful Mexican street food with marinated grilled chicken, fresh vegetables, and zesty lime. Light yet satisfying, great for summer evenings.",
            Ingredients = "chicken breast, tortillas, lime, cilantro, onion, garlic, chili powder, cumin",
            CookingTime = "30 minutes",
            Difficulty = "Easy"
    },
    new() {
            Key = 2,
            Name = "Creamy Tomato Soup",
            Description = "Warm, comforting soup perfect for cold days. Smooth and velvety with a rich tomato flavor and a hint of basil. Great with grilled cheese sandwiches.",
            Ingredients = "tomatoes, heavy cream, onion, garlic, basil, olive oil, vegetable broth",
            CookingTime = "40 minutes",
            Difficulty = "Easy"
    },
    new() {
            Key = 3,
            Name = "Beef Stir-Fry with Vegetables",
            Description = "Quick Asian-inspired dish with tender beef strips and crisp vegetables in a savory sauce. Healthy, colorful, and packed with flavor. Serve over rice.",
            Ingredients = "beef sirloin, bell peppers, broccoli, soy sauce, ginger, garlic, sesame oil, rice",
            CookingTime = "25 minutes",
            Difficulty = "Easy"
    },
    new() {
            Key = 4,
            Name = "Chocolate Lava Cake",
            Description = "Decadent dessert with a molten chocolate center that flows out when you cut into it. Rich, indulgent, and impressively elegant. Perfect for special occasions.",
            Ingredients = "dark chocolate, butter, eggs, sugar, flour, vanilla extract",
            CookingTime = "15 minutes",
            Difficulty = "Hard"
    },
    new() {
            Key = 5,
            Name = "Caesar Salad",
            Description = "Classic crispy romaine lettuce with creamy Caesar dressing, crunchy croutons, and Parmesan cheese. Light but satisfying, can add grilled chicken for protein.",
            Ingredients = "romaine lettuce, Parmesan cheese, croutons, Caesar dressing, lemon, garlic, anchovies",
            CookingTime = "15 minutes",
            Difficulty = "Easy"
    },
    new() {
            Key = 6,
            Name = "Vegetarian Chickpea Curry",
            Description = "Hearty Indian curry with tender chickpeas in a spiced tomato sauce with coconut milk. Warming, aromatic, and full of complex flavors. Great for meal prep.",
            Ingredients = "chickpeas, coconut milk, tomatoes, onion, garlic, curry powder, cumin, turmeric, rice",
            CookingTime = "35 minutes",
            Difficulty = "Medium"
    },
    new() {
            Key = 7,
            Name = "Banana Pancakes",
            Description = "Fluffy breakfast pancakes with sweet mashed banana mixed into the batter. Naturally sweet, kid-friendly, and perfect for lazy weekend mornings. Serve with maple syrup.",
            Ingredients = "bananas, flour, eggs, milk, baking powder, butter, maple syrup",
            CookingTime = "20 minutes",
            Difficulty = "Easy"
    }
];

// Generate embeddings for all recipes
// This is where the magic happens! We convert each recipe description into a vector (list of numbers)
// The AI model reads the description and creates a mathematical representation of its meaning
Console.WriteLine("Generating embeddings for recipes...\n");
foreach (Recipe recipe in recipes)
{
    // Send the recipe description to Azure OpenAI
    var embeddingResponse = await embeddingClient.GenerateEmbeddingsAsync(new List<string> { recipe.Description });
    // Store the vector (embedding) in the recipe
    recipe.Vector = embeddingResponse.Value[0].ToFloats();
    Console.WriteLine($"Added: {recipe.Name}");
}

// Interactive search loop
// Now users can search for recipes using natural language!
Console.WriteLine("\n=== Recipe Search Assistant ===");
Console.WriteLine("Try searching like: 'something quick and easy' or 'comfort food' or 'healthy dinner'\n");
Console.WriteLine("Enter a search query (or 'exit' to quit):\n");

while (true)
{
    Console.Write("> ");
    string? userQuery = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(userQuery) || userQuery.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    // Generate embedding for the search query
    var queryEmbeddingResponse = await embeddingClient.GenerateEmbeddingsAsync(new List<string> { userQuery });
    var queryVector = queryEmbeddingResponse.Value[0].ToFloats().ToArray();

    // Perform manual similarity search
    // We compare the query vector with each recipe's vector using cosine similarity
    // Higher score = more similar meaning
    var similarities = recipes.Select(recipe => new
    {
        Recipe = recipe,
        Score = CosineSimilarity(queryVector, recipe.Vector.ToArray())
    })
    .OrderByDescending(x => x.Score)  // Sort by most similar first
    .Take(3);  // Get top 3 matches

    Console.WriteLine($"\n🍳 Top 3 recipe matches for '{userQuery}':\n");
    
    foreach (var result in similarities)
    {
        // Display the recipe with its details
        Console.WriteLine($"  [{result.Score:F4}] {result.Recipe.Name}");
        Console.WriteLine($"  {result.Recipe.Description}");
        Console.WriteLine($"  ⏱️  {result.Recipe.CookingTime} | 📊 {result.Recipe.Difficulty}");
        Console.WriteLine($"  🥘 Ingredients: {result.Recipe.Ingredients}\n");
    }
}

// Helper method to calculate cosine similarity
static double CosineSimilarity(float[] vector1, float[] vector2)
{
    if (vector1.Length != vector2.Length)
        throw new ArgumentException("Vectors must have the same length");

    double dotProduct = 0;
    double magnitude1 = 0;
    double magnitude2 = 0;

    for (int i = 0; i < vector1.Length; i++)
    {
        dotProduct += vector1[i] * vector2[i];
        magnitude1 += vector1[i] * vector1[i];
        magnitude2 += vector2[i] * vector2[i];
    }

    magnitude1 = Math.Sqrt(magnitude1);
    magnitude2 = Math.Sqrt(magnitude2);

    if (magnitude1 == 0 || magnitude2 == 0)
        return 0;

    return dotProduct / (magnitude1 * magnitude2);
}