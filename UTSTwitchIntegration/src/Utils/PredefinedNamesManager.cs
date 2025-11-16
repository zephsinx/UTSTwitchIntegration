using System;
using System.Collections.Generic;
using System.IO;

namespace UTSTwitchIntegration.Utils
{
    /// <summary>
    /// Predefined names manager for loading names from a text file
    /// </summary>
    public class PredefinedNamesManager
    {
        private static PredefinedNamesManager instance;
        private static readonly object InstanceLock = new object();

        private List<string> names;
        private readonly Random random;
        private readonly object namesLock = new object();

        /// <summary>
        /// Singleton instance of PredefinedNamesManager
        /// </summary>
        public static PredefinedNamesManager Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                lock (InstanceLock)
                {
                    instance ??= new PredefinedNamesManager();
                }
                return instance;
            }
        }

        /// <summary>
        /// Number of names loaded
        /// </summary>
        public int Count
        {
            get
            {
                lock (this.namesLock)
                {
                    return this.names?.Count ?? 0;
                }
            }
        }

        private PredefinedNamesManager()
        {
            this.names = new List<string>();
            this.random = new Random();
        }

        /// <summary>
        /// Resolve file path relative to game directory
        /// </summary>
        /// <param name="filePath">Path to resolve (relative or absolute)</param>
        private string ResolveFilePath(string filePath)
        {
            if (Path.IsPathRooted(filePath))
            {
                return filePath;
            }

            if (!filePath.StartsWith("UserData/", StringComparison.OrdinalIgnoreCase) &&
                !filePath.StartsWith("UserData\\", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(MelonLoader.Utils.MelonEnvironment.GameRootDirectory, filePath);
            string relativePath = filePath[9..];
            return Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, relativePath);

        }

        /// <summary>
        /// Create default predefined names file
        /// </summary>
        /// <param name="filePath">Absolute path where to create the file</param>
        private static void CreateDefaultNamesFile(string filePath)
        {
            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string[] defaultNames = {
                    "Emory", "Pascale", "Augustus", "Felipa", "Nadia", "Karine", "Reyna", "Monty", "Abbie", "Concepcion", "Brown", "Marquise", "Darrell", "Xzavier", "Ella", "Greg", "Keyon", "Shyanne", "Nannie", "Ambrose", "Doyle", "Jaime", "Connor", "Tad", "Rafaela", "Santa", "Meghan", "Julianne", "Kenton", "Eileen", "Trudie", "Marlee", "Priscilla", "Gustave", "Sofia", "Giovanna", "Rod", "Karen", "Monte", "Dulce", "Lia", "Della", "Ayla", "Leonard", "Dortha", "Jay", "Gennaro", "Timothy", "Houston", "Kamille", "Narciso", "Ronny", "Destany", "Darrick", "Vicenta", "Virginie", "Brisa", "Anahi", "Cleveland", "Sienna", "Adrien", "Reese", "Lorenzo", "Roxane", "Rosina", "Elinore", "Zoe", "Hortense", "Catalina", "Alejandra", "Jasmin", "Rickey", "Kayley", "Marley", "Amaya", "Dorian", "Cody", "Candace", "Stevie", "Caterina", "Nannie", "Rhoda", "Houston", "Everett", "Rico", "Evelyn", "Malvina", "Harvey", "Sam", "Joey", "Connie", "Mac", "Opal", "Cydney", "Allen", "Chelsea", "Johnpaul", "Myrl", "Tomasa", "Samir", "Elna", "Simone", "Casandra", "Turner", "Genoveva", "Robert", "Mateo", "Okey", "Eleanore", "Veronica", "Jordon", "Verna", "Malinda", "Oswaldo", "Candida", "Lenna", "Meredith", "Jasmin", "Roderick", "Emely", "Rickie", "Julie", "Andrew", "Vaughn", "Gonzalo", "Edwardo", "Ruben", "Emelia", "Oswald", "Edgar", "Tania", "Eleazar", "Ottis", "Stephanie", "Freda", "Keith", "Melba", "Norval", "Omari", "Imogene", "Haleigh", "Lorenz", "Delphine", "Chester", "Domenick", "Dale", "Xavier", "Leone", "Virgil", "Evalyn", "Julia", "Jonas", "Bernadine", "Cortney", "Nettie", "Rory", "Marjolaine", "Elvera", "Daphnee", "Rex", "Doris", "Louie", "Claire", "Aisha", "Allie", "Winston", "Valentine", "Cathy", "Fred", "Shanny", "Nyasia", "Edna", "Mariela", "Florencio", "Thalia", "Micheal", "Natalia", "Kyla", "Marion", "Shakira", "Favian", "Carolanne", "Vita", "Cale", "Chester", "Tyra", "Maxwell", "Kamron", "Carrie", "Virgie", "Meta", "Micheal", "Eunice", "Deion", "Horace", "Jackeline", "Cedrick", "Rosella", "Nannie", "Onie", "Adelle", "Luigi", "Brian", "Lucius", "Ida", "Mercedes", "Octavia", "Gilbert", "Icie", "Hassan", "Cruz", "Earline", "Toney", "Imani", "Kaden", "Chasity", "Vladimir", "Minerva", "Consuelo", "Benjamin", "Novella", "Cecilia", "Patience", "Roslyn", "Camren", "Eliezer", "Dorthy", "Adrain", "Genoveva", "Michale", "Julia", "Henry", "Crystel", "Lewis", "Isaias", "Geovanni", "Rowan", "Samara", "Kayla", "Sallie", "Albin", "Estrella", "Stephania", "Beth", "Cecile", "Nat", "Lenore", "General", "Lauretta", "Donna", "Chet", "Tanner", "Jerome", "Coby", "Annamae", "Aurelie", "Hosea", "Shanelle", "Reginald", "Dee", "Lessie", "Evans", "Russell", "Geovany", "Aurelie", "Kelli", "Guy", "Libbie", "Erika", "Arnulfo", "Terrill", "Lelia", "Vernie", "Neha", "Jedediah", "Ivah", "Luz", "Etha", "London", "Samara", "Jabari", "Mariana", "Keshawn", "Serenity", "Elijah", "Kennith", "Grant", "Geovanni", "Shaniya", "Jaeden", "Nella", "Stefanie", "Maximillian", "Joseph", "Magnus", "Camylle", "Jadyn", "Gerda", "Felipe", "Jalyn", "Shemar", "Maxwell", "Sophie", "Sophie", "Makayla", "Barbara", "Frederik", "Ardith", "Nels", "Maurice", "Orville", "Elvera", "Erica", "Yvette", "Kellen", "Matilde", "Chaim", "Mable", "Lilly", "Marcella", "Dagmar", "Zachariah", "Chadd", "Lilliana", "Lauriane", "Delta", "Friedrich", "Sven", "Hardy", "Mable", "Myles", "Cora", "Thea", "Leola", "Lottie", "Eileen", "Alejandrin", "Layla", "Darian", "Meda", "Elsa", "Jovany", "Mona", "Camryn", "Kaden", "Raquel", "Lola", "Izaiah", "Lucile", "Randy", "Allison", "Felix", "Francisca", "Ernest", "Cecil", "Jedidiah", "Jalen", "Aliza", "Evalyn", "Agustina", "Prince", "Price", "Darwin", "D'angelo", "Osvaldo", "Lina", "Loren", "Bella", "Constantin", "Hilario", "Adelle", "Alan", "Estella", "Hillary", "Edward", "Arno", "Una", "Esmeralda", "Maia", "Trycia", "Anastacio", "Orpha", "Vidal", "Elinore", "Letha", "Jonathan", "Sid", "Valentine", "Amira", "Leola", "Lucio", "Nola", "Garfield", "Makenzie", "Aubrey", "Lorenz", "Horacio", "Murray", "Lucinda", "Bret", "Tyson", "Priscilla", "Heidi", "Bernie", "Elva", "Brendon", "Carey", "Charley", "Randall", "Ashly", "Brianne", "Edd", "Brielle", "Antonio", "Ceasar", "Delphine", "Wilson", "Caleb", "Laurel", "Alanna", "Chet", "Carmine", "Sidney", "Angelo", "Elliott", "Greg", "Jennyfer", "Lisa", "Ava", "Jalen", "Mortimer", "Johathan", "Markus", "Jesse", "Rosalind", "Eldridge", "Emile", "Blaise", "Elda", "Chasity", "Reggie", "Lily", "Arely", "Arnold", "Abbigail", "Mariam", "Misael", "Jaiden", "Fredy", "Jayme", "Ardella", "Genoveva", "Jesus", "Akeem", "Tanner", "Dock", "Giuseppe", "Koby", "Josie", "Tiara", "Jaren", "Marilyne", "Kaylie", "Brionna", "Nolan", "Ansley", "Ransom", "Zelma", "Shania", "Ashleigh", "Eryn", "Hershel", "Immanuel", "Ashleigh", "Jovani", "Joshua", "Lora", "Michelle", "Justine", "Angel", "Brice", "Verdie", "Brent", "Kayden", "Rashad", "Neva", "Paula", "Malika", "Stephany", "Shawn", "Jayden", "Bradford", "Roel", "Owen", "Sonia", "Dustin", "Bernhard", "Vergie", "Adrien", "Berenice",
                };

                File.WriteAllLines(filePath, defaultNames);
                Logger.Info($"Created default predefined names file with {defaultNames.Length} names at: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create default predefined names file: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Load names from a text file (one name per line)
        /// </summary>
        /// <param name="filePath">Path to the names file</param>
        public bool LoadNamesFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Logger.Error("Cannot load predefined names: file path is null or empty");
                return false;
            }

            try
            {
                string resolvedPath = ResolveFilePath(filePath);

                if (!File.Exists(resolvedPath))
                {
                    Logger.Info($"Predefined names file not found, creating default file at: {resolvedPath}");
                    CreateDefaultNamesFile(resolvedPath);
                }

                string[] lines = File.ReadAllLines(resolvedPath);
                List<string> validNames = new List<string>();

                foreach (string line in lines)
                {
                    string trimmedLine = line?.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        validNames.Add(trimmedLine);
                    }
                }

                lock (this.namesLock)
                {
                    this.names = validNames;
                }

                if (validNames.Count > 0)
                {
                    Logger.Info($"Loaded {validNames.Count} predefined names from {resolvedPath}");
                    return true;
                }

                Logger.Warning($"Predefined names file is empty or contains no valid names: {resolvedPath}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error($"Access denied when reading predefined names file: {filePath}");
                Logger.Debug($"Exception: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Logger.Error($"IO error reading predefined names file: {filePath}");
                Logger.Debug($"Exception: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error loading predefined names from {filePath}: {ex.Message}");
                Logger.Debug($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Get a random name from the loaded names list
        /// </summary>
        public string GetRandomName()
        {
            lock (this.namesLock)
            {
                if (this.names == null || this.names.Count == 0)
                {
                    return null;
                }

                int randomIndex = this.random.Next(this.names.Count);
                return this.names[randomIndex];
            }
        }
    }
}

