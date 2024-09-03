using System;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public class OpenAIClient
{
    private readonly string endpoint;

    public OpenAIClient(string endpoint)
    {
        this.endpoint = endpoint;
    }

    public async Task<string> GetChatResponse(string userMessage, object[] messages)
    {
        using (HttpClient client = new HttpClient())
        {
            // Prepara la solicitud
            var requestBody = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You're an assistant that generates SQL queries based on user commands. Handle table names, columns, and query types dynamically. Do not include 'SQL' or 'sql' as a prefix, only the SQL query itself."
                    }
                }
                .Concat(messages) // Agrega el historial de mensajes
                .Concat(new[]
                {
                    new
                    {
                        role = "user",
                        content = userMessage
                    }
                })
            };

            string jsonRequestBody = JsonConvert.SerializeObject(requestBody);
            HttpContent content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

            // Envía la solicitud POST
            HttpResponseMessage response = await client.PostAsync(endpoint, content);

            // Verifica si la respuesta es exitosa
            response.EnsureSuccessStatusCode();

            // Lee la respuesta
            string responseBody = await response.Content.ReadAsStringAsync();
            return responseBody;
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        string endpoint = "https://ia-prueba.openai.azure.com/openai/deployments/PruebaRivel/chat/completions?api-version=2023-03-15-preview&Content-Type=application/json&api-key=9974484720ef494bae9a418878d4b7e6";

        OpenAIClient client = new OpenAIClient(endpoint);

        var messages = new[]
        {
            new
            {
                role = "system",
                content = "You're an assistant that generates SQL queries based on user commands. Handle table names, columns, and query types dynamically. Do not include 'SQL' or 'sql' as a prefix, only the SQL query itself."
            }
        };

        Console.WriteLine("Chatbot: How can I assist you with SQL queries today?");

        while (true)
        {
            Console.Write("You: ");
            string userMessage = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userMessage))
                break;

            // Agrega el mensaje del usuario al historial
            var conversation = messages.Concat(new[]
            {
                new
                {
                    role = "user",
                    content = userMessage
                }
            }).ToArray();

            // Obtén la respuesta del chatbot
            string response = await client.GetChatResponse(userMessage, conversation);
            var responseObject = JsonConvert.DeserializeObject<dynamic>(response);
            string sqlQuery = responseObject.choices[0].message.content;

            // Elimina cualquier ocurrencia de 'SQL' o 'sql' dentro de la consulta
            sqlQuery = RemoveSqlPrefixes(sqlQuery);

            Console.WriteLine($"Chatbot: {sqlQuery}");

            // Ejecuta la consulta SQL
            string queryResult = await ExecuteSqlQuery(sqlQuery);

            // Muestra el resultado
            Console.WriteLine("\n \n \n SQL Query Result: ");
            Console.WriteLine($"Chatbot:\n {queryResult}");

            // Actualiza el historial de mensajes
            messages = conversation.Concat(new[]
            {
                new
                {
                    role = "assistant",
                    content = sqlQuery
                }
            }).ToArray();
        }
    }

    static string RemoveSqlPrefixes(string input)
    {
        // Elimina 'SQL' o 'sql' y cualquier espacio adyacente
        string cleanedQuery = Regex.Replace(input, @"\b(sql|SQL)\b\s*", "", RegexOptions.IgnoreCase);

        // Elimina caracteres ``` al inicio y al final
        cleanedQuery = cleanedQuery.Trim('`').Trim();

        return cleanedQuery;
    }

    static async Task<string> ExecuteSqlQuery(string sqlQuery)
    {
        string connectionString = "Server=tcp:aplix-bd-desarrollo.database.windows.net,1433;Initial Catalog=WB_APLIX;Persist Security Info=False;User ID=admin-db;Password=Sql2016!!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(sqlQuery, connection);
                connection.Open();

                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    var result = new StringBuilder();

                    // Construye el encabezado de columnas
                    string[] columnNames = Enumerable.Range(0, reader.FieldCount)
                                                     .Select(i => reader.GetName(i))
                                                     .ToArray();
                    result.AppendLine(string.Join(" | ", columnNames));

                    while (await reader.ReadAsync())
                    {
                        // Construye la fila con valores de columnas
                        var rowValues = columnNames.Select(col => reader[col]?.ToString() ?? "NULL");
                        result.AppendLine(string.Join(" | ", rowValues));
                    }

                    return result.ToString();
                }
            }
        }
        catch (SqlException ex)
        {
            // Maneja errores específicos de SQL
            return $"Error executing SQL query: {ex.Message}";
        }
        catch (Exception ex)
        {
            // Maneja otros errores
            return $"An unexpected error occurred: {ex.Message}";
        }
    }
}
