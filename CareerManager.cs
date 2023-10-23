using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace DarkbulbBot
{
    class CareerManager
    {
        private static readonly Dictionary<string, Career> activeCareers = new Dictionary<string, Career>();
        private static readonly string CareerFilePath = "careers.json";
        public static void AddCareer(Career career) 
        {
            if (career == null || string.IsNullOrWhiteSpace(career.ID)) 
            {
                Console.WriteLine($"Error: Tried to add null career");
                return;
            }
            if (activeCareers.ContainsKey(career.ID))
            {
                Console.WriteLine($"Error: Tried to add career that already exists '{career.ID}'");
                return;
            }
            else 
            {
                activeCareers.Add(career.ID, career);
            }
        }

        public static void RemoveCareer(string ID) 
        {
            if (string.IsNullOrWhiteSpace(ID))
            {
                Console.WriteLine($"Error: Null or invalid ID provided in GetCareer");
                return;
            }

            if (activeCareers.ContainsKey(ID))
            {
                activeCareers.Remove(ID);
            }
            else 
            {
                Console.WriteLine($"Error: Tried to remove ID that does not exist '{ID}'");
            }
        }

        public static Career GetCareer(string ID) 
        {
            if (string.IsNullOrWhiteSpace(ID)) 
            {
                Console.WriteLine($"Error: Null or invalid ID provided in GetCareer");
                return null;
            }

            if (activeCareers.ContainsKey(ID)) 
            {
                return activeCareers[ID];
            }
            else
            {
                return null;
            }
        }

        public static void SaveCareers()
        {
            try
            {
                var jsonData = JsonConvert.SerializeObject(activeCareers, Formatting.Indented);
                File.WriteAllText(CareerFilePath, jsonData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while saving careers: {ex.Message}");
            }
        }

        public static void LoadCareers()
        {
            try
            {
                if (File.Exists(CareerFilePath))
                {
                    var jsonData = File.ReadAllText(CareerFilePath);
                    var careers = JsonConvert.DeserializeObject<Dictionary<string, Career>>(jsonData);
                    if (careers != null)
                    {
                        foreach (var career in careers)
                        {
                            activeCareers[career.Key] = career.Value;
                        }
                    }
                    else 
                    {
                        Console.WriteLine($"Failed to deserialize careers json");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while loading careers: {ex.Message}");
            }
        }
        public static Dictionary<string, Career> GetActiveCareers()
        {
            return new Dictionary<string, Career>(activeCareers);
        }
    }
}
