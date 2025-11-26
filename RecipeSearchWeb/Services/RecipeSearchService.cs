using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Embeddings;
using OpenAI.Embeddings;
using RecipeSearchWeb.Models;

namespace RecipeSearchWeb.Services;

/// <summary>
/// Service that handles recipe searching using AI embeddings
/// This is where all the magic happens!
/// </summary>
public class RecipeSearchService
{
    private readonly EmbeddingClient _embeddingClient;
    private List<Recipe> _recipes = new();
    private bool _isInitialized = false;

    public RecipeSearchService(EmbeddingClient embeddingClient)
    {
        _embeddingClient = embeddingClient;
    }

    /// <summary>
    /// Initialize the service by loading recipes and generating embeddings
    /// This happens once when the app starts
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        // Load all our recipes
        _recipes = GetAllRecipes();

        // Generate embeddings for each recipe
        // This converts descriptions into vectors (numbers) that the AI can compare
        foreach (var recipe in _recipes)
        {
            var embeddingResponse = await _embeddingClient.GenerateEmbeddingsAsync(new List<string> { recipe.Description });
            recipe.Vector = embeddingResponse.Value[0].ToFloats();
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Search for recipes based on a natural language query
    /// </summary>
    public async Task<List<Recipe>> SearchRecipesAsync(string query, int topResults = 6)
    {
        if (string.IsNullOrWhiteSpace(query))
            return _recipes.Take(topResults).ToList();

        // Convert the search query to a vector
        var queryEmbeddingResponse = await _embeddingClient.GenerateEmbeddingsAsync(new List<string> { query });
        var queryVector = queryEmbeddingResponse.Value[0].ToFloats().ToArray();

        // Find the most similar recipes using cosine similarity
        var results = _recipes
            .Select(recipe => new
            {
                Recipe = recipe,
                Score = CosineSimilarity(queryVector, recipe.Vector.ToArray())
            })
            .OrderByDescending(x => x.Score)
            .Take(topResults)
            .Select(x => x.Recipe)
            .ToList();

        return results;
    }

    /// <summary>
    /// Get all recipes without search
    /// </summary>
    public List<Recipe> GetAllRecipes()
    {
        return new List<Recipe>
        {
            // BREAKFAST RECIPES
            new() {
                Key = 0,
                Name = "Banana Pancakes",
                Category = "Breakfast",
                Description = "Fluffy breakfast pancakes with sweet mashed banana mixed into the batter. Naturally sweet, kid-friendly, and perfect for lazy weekend mornings. Serve with maple syrup.",
                Ingredients = "bananas, flour, eggs, milk, baking powder, butter, maple syrup",
                CookingTime = "20 minutes",
                Difficulty = "Easy",
                Instructions = @"1. Mash 2 ripe bananas in a large bowl until smooth.
2. Whisk in 2 eggs and 1 cup of milk until well combined.
3. Add 1½ cups flour and 2 tsp baking powder, stir until just combined (don't overmix).
4. Heat a non-stick pan over medium heat and add a little butter.
5. Pour ¼ cup batter for each pancake and cook until bubbles form on surface (2-3 minutes).
6. Flip and cook another 2 minutes until golden brown.
7. Serve hot with butter and maple syrup. Enjoy!"
            },
            new() {
                Key = 1,
                Name = "Avocado Toast with Poached Eggs",
                Category = "Breakfast",
                Description = "Trendy and nutritious breakfast with creamy avocado on toasted sourdough, topped with perfectly poached eggs. Healthy fats and protein to start your day right.",
                Ingredients = "avocado, sourdough bread, eggs, lemon juice, red pepper flakes, olive oil, salt",
                CookingTime = "15 minutes",
                Difficulty = "Medium",
                Instructions = @"1. Bring a pot of water to a gentle simmer, add a splash of vinegar.
2. Toast 2 slices of sourdough bread until golden and crispy.
3. Mash 1 ripe avocado with lemon juice, salt, and pepper.
4. Crack eggs into the simmering water and poach for 3-4 minutes.
5. Spread avocado mixture generously on toasted bread.
6. Top with poached eggs and drizzle with olive oil.
7. Sprinkle with red pepper flakes and sea salt. Serve immediately!"
            },
            new() {
                Key = 2,
                Name = "Berry Smoothie Bowl",
                Category = "Breakfast",
                Description = "Vibrant and refreshing breakfast bowl packed with antioxidants. Thick smoothie topped with granola, fresh berries, and coconut flakes. Instagram-worthy and delicious.",
                Ingredients = "mixed berries, banana, Greek yogurt, granola, honey, coconut flakes, chia seeds",
                CookingTime = "10 minutes",
                Difficulty = "Easy"
            },
            new() {
                Key = 3,
                Name = "French Toast with Cinnamon",
                Category = "Breakfast",
                Description = "Classic brunch favorite with crispy edges and soft center. Bread soaked in cinnamon-spiced egg mixture and pan-fried to golden perfection. Indulgent weekend treat.",
                Ingredients = "bread, eggs, milk, cinnamon, vanilla extract, butter, powdered sugar, maple syrup",
                CookingTime = "25 minutes",
                Difficulty = "Easy"
            },

            // LUNCH RECIPES
            new() {
                Key = 4,
                Name = "Grilled Chicken Caesar Salad",
                Category = "Lunch",
                Description = "Classic crispy romaine lettuce with creamy Caesar dressing, crunchy croutons, and Parmesan cheese. Topped with perfectly grilled chicken breast for protein. Light but satisfying.",
                Ingredients = "chicken breast, romaine lettuce, Parmesan cheese, croutons, Caesar dressing, lemon, garlic",
                CookingTime = "25 minutes",
                Difficulty = "Easy"
            },
            new() {
                Key = 5,
                Name = "Mediterranean Quinoa Bowl",
                Category = "Lunch",
                Description = "Healthy grain bowl with fluffy quinoa, cherry tomatoes, cucumbers, feta cheese, and olives. Drizzled with lemon herb dressing. Perfect for meal prep.",
                Ingredients = "quinoa, cherry tomatoes, cucumber, feta cheese, olives, red onion, lemon, olive oil, oregano",
                CookingTime = "30 minutes",
                Difficulty = "Easy"
            },
            new() {
                Key = 6,
                Name = "Caprese Sandwich",
                Category = "Lunch",
                Description = "Fresh Italian sandwich with ripe tomatoes, creamy mozzarella, and fragrant basil on ciabatta bread. Drizzled with balsamic glaze. Simple yet elegant.",
                Ingredients = "ciabatta bread, mozzarella, tomatoes, fresh basil, balsamic vinegar, olive oil, salt, pepper",
                CookingTime = "10 minutes",
                Difficulty = "Easy"
            },
            new() {
                Key = 7,
                Name = "Vietnamese Pho Soup",
                Category = "Lunch",
                Description = "Aromatic beef noodle soup with rich broth, rice noodles, and tender beef slices. Topped with fresh herbs, lime, and bean sprouts. Comforting and flavorful.",
                Ingredients = "beef bones, rice noodles, beef sirloin, star anise, cinnamon, ginger, fish sauce, cilantro, lime, bean sprouts",
                CookingTime = "2 hours",
                Difficulty = "Hard"
            },

            // DINNER RECIPES
            new() {
                Key = 8,
                Name = "Classic Spaghetti Carbonara",
                Category = "Dinner",
                Description = "A creamy Italian pasta dish with eggs, cheese, pancetta, and black pepper. Rich, comforting, and authentic Roman cuisine. Perfect for a quick but impressive dinner.",
                Ingredients = "spaghetti, eggs, pancetta, Parmesan cheese, black pepper, salt",
                CookingTime = "20 minutes",
                Difficulty = "Medium"
            },
            new() {
                Key = 9,
                Name = "Grilled Salmon with Lemon Butter",
                Category = "Dinner",
                Description = "Elegant seafood dish with perfectly grilled salmon fillet in a tangy lemon butter sauce. Healthy omega-3s and restaurant-quality flavor. Pairs well with asparagus.",
                Ingredients = "salmon fillet, lemon, butter, garlic, dill, white wine, olive oil",
                CookingTime = "25 minutes",
                Difficulty = "Medium"
            },
            new() {
                Key = 10,
                Name = "Beef Stir-Fry with Vegetables",
                Category = "Dinner",
                Description = "Quick Asian-inspired dish with tender beef strips and crisp vegetables in a savory sauce. Healthy, colorful, and packed with flavor. Serve over rice.",
                Ingredients = "beef sirloin, bell peppers, broccoli, soy sauce, ginger, garlic, sesame oil, rice",
                CookingTime = "25 minutes",
                Difficulty = "Easy"
            },
            new() {
                Key = 11,
                Name = "Vegetarian Chickpea Curry",
                Category = "Dinner",
                Description = "Hearty Indian curry with tender chickpeas in a spiced tomato sauce with coconut milk. Warming, aromatic, and full of complex flavors. Great for meal prep.",
                Ingredients = "chickpeas, coconut milk, tomatoes, onion, garlic, curry powder, cumin, turmeric, rice",
                CookingTime = "35 minutes",
                Difficulty = "Medium"
            },
            new() {
                Key = 12,
                Name = "Grilled Chicken Tacos",
                Category = "Dinner",
                Description = "Spicy and flavorful Mexican street food with marinated grilled chicken, fresh vegetables, and zesty lime. Light yet satisfying, great for summer evenings.",
                Ingredients = "chicken breast, tortillas, lime, cilantro, onion, garlic, chili powder, cumin, avocado, salsa",
                CookingTime = "30 minutes",
                Difficulty = "Easy"
            },
            new() {
                Key = 13,
                Name = "Mushroom Risotto",
                Category = "Dinner",
                Description = "Luxurious Italian rice dish cooked slowly with white wine, mushrooms, and Parmesan. Creamy, earthy, and comforting. Requires patience but worth every stir.",
                Ingredients = "arborio rice, mushrooms, white wine, Parmesan cheese, butter, onion, garlic, vegetable broth",
                CookingTime = "45 minutes",
                Difficulty = "Hard"
            },
            new() {
                Key = 14,
                Name = "BBQ Pulled Pork Sandwich",
                Category = "Dinner",
                Description = "Slow-cooked pork shoulder in tangy BBQ sauce, tender enough to pull apart with a fork. Served on soft buns with coleslaw. Ultimate comfort food.",
                Ingredients = "pork shoulder, BBQ sauce, brown sugar, apple cider vinegar, coleslaw, hamburger buns, paprika",
                CookingTime = "6 hours",
                Difficulty = "Medium"
            },

            // DESSERTS
            new() {
                Key = 15,
                Name = "Chocolate Lava Cake",
                Category = "Dessert",
                Description = "Decadent dessert with a molten chocolate center that flows out when you cut into it. Rich, indulgent, and impressively elegant. Perfect for special occasions.",
                Ingredients = "dark chocolate, butter, eggs, sugar, flour, vanilla extract",
                CookingTime = "15 minutes",
                Difficulty = "Hard"
            },
            new() {
                Key = 16,
                Name = "New York Cheesecake",
                Category = "Dessert",
                Description = "Iconic dense and creamy cheesecake with a graham cracker crust. Smooth texture with subtle tang from cream cheese. Classic American dessert.",
                Ingredients = "cream cheese, sugar, eggs, sour cream, vanilla extract, graham crackers, butter",
                CookingTime = "1 hour 30 minutes",
                Difficulty = "Hard"
            },
            new() {
                Key = 17,
                Name = "Tiramisu",
                Category = "Dessert",
                Description = "Classic Italian dessert with layers of coffee-soaked ladyfingers and mascarpone cream. Dusted with cocoa powder. Elegant and sophisticated.",
                Ingredients = "ladyfingers, mascarpone cheese, eggs, sugar, espresso, cocoa powder, coffee liqueur",
                CookingTime = "30 minutes + chilling",
                Difficulty = "Medium"
            },
            new() {
                Key = 18,
                Name = "Apple Pie",
                Category = "Dessert",
                Description = "Traditional American dessert with spiced apple filling in a flaky butter crust. Warm, comforting, and perfect with vanilla ice cream. Tastes like home.",
                Ingredients = "apples, flour, butter, sugar, cinnamon, nutmeg, lemon juice, vanilla ice cream",
                CookingTime = "1 hour 15 minutes",
                Difficulty = "Hard"
            },
            new() {
                Key = 19,
                Name = "Chocolate Chip Cookies",
                Category = "Dessert",
                Description = "Classic chewy cookies with melty chocolate chips. Crispy edges and soft centers. The ultimate comfort snack that everyone loves.",
                Ingredients = "flour, butter, brown sugar, white sugar, eggs, vanilla extract, chocolate chips, baking soda",
                CookingTime = "25 minutes",
                Difficulty = "Easy"
            },

            // APPETIZERS
            new() {
                Key = 20,
                Name = "Bruschetta",
                Category = "Appetizer",
                Description = "Italian starter with toasted bread topped with fresh tomatoes, basil, and garlic. Simple, fresh, and bursting with flavor. Perfect for parties.",
                Ingredients = "baguette, tomatoes, fresh basil, garlic, olive oil, balsamic vinegar, salt",
                CookingTime = "15 minutes",
                Difficulty = "Easy"
            },
            new() {
                Key = 21,
                Name = "Buffalo Chicken Wings",
                Category = "Appetizer",
                Description = "Spicy and tangy chicken wings tossed in buffalo sauce. Crispy skin, tender meat. Game day essential with blue cheese dip and celery sticks.",
                Ingredients = "chicken wings, buffalo sauce, butter, blue cheese dressing, celery, hot sauce",
                CookingTime = "45 minutes",
                Difficulty = "Medium"
            },
            new() {
                Key = 22,
                Name = "Spinach and Artichoke Dip",
                Category = "Appetizer",
                Description = "Creamy, cheesy dip loaded with spinach and artichokes. Warm and gooey, perfect for dipping chips or bread. Party favorite that disappears quickly.",
                Ingredients = "spinach, artichoke hearts, cream cheese, sour cream, Parmesan cheese, mozzarella, garlic",
                CookingTime = "30 minutes",
                Difficulty = "Easy"
            },

            // SNACKS
            new() {
                Key = 23,
                Name = "Homemade Hummus",
                Category = "Snack",
                Description = "Smooth and creamy Middle Eastern dip made from chickpeas and tahini. Healthy protein-packed snack perfect with vegetables or pita bread.",
                Ingredients = "chickpeas, tahini, lemon juice, garlic, olive oil, cumin, paprika",
                CookingTime = "10 minutes",
                Difficulty = "Easy"
            },
            new() {
                Key = 24,
                Name = "Energy Balls",
                Category = "Snack",
                Description = "No-bake healthy snacks with oats, dates, and nuts. Naturally sweet, portable, and perfect for pre-workout fuel or afternoon pick-me-up.",
                Ingredients = "dates, oats, almonds, cocoa powder, honey, coconut flakes, chia seeds",
                CookingTime = "15 minutes",
                Difficulty = "Easy"
            }
        };
    }

    /// <summary>
    /// Calculate how similar two vectors are using cosine similarity
    /// Returns a score between 0 (completely different) and 1 (identical)
    /// </summary>
    private static double CosineSimilarity(float[] vector1, float[] vector2)
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
}
