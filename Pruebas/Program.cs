using System;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
                        content = "you're a helpful assistant that talks like a pirate"
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

            // Envía la solicitud POST usando la URL completa con la clave de API
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
        string apiKey = "9974484720ef494bae9a418878d4b7e6"; // Reemplaza con tu clave de API
        string endpoint = "https://ia-prueba.openai.azure.com/openai/deployments/PruebaRivel/chat/completions?api-version=2023-03-15-preview"; // Usa el endpoint correcto

        OpenAIClient client = new OpenAIClient(endpoint);

        // Historial de mensajes
        var messages = new[]
        {
            new
            {
                role = "system",
                content = "you're a helpful assistant that talks like a pirate"
            }
        };

        Console.WriteLine("Chatbot: Ahoy! How can I assist ye today?");

        while (true)
        {
            Console.Write("You: ");
            string userMessage = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userMessage))
                break;

            // Comprobar si el usuario quiere obtener un reporte de la base de datos
            if (userMessage.Trim().ToLower().StartsWith("dame un reporte de la bd"))
            {
                // Analiza el mensaje para obtener la cantidad de registros
                string[] parts = userMessage.Split(new[] { " ", "de", "la", "bd" }, StringSplitOptions.RemoveEmptyEntries);
                int numberOfRecords = 10; // Valor predeterminado

                if (parts.Length > 1 && int.TryParse(parts[1], out int parsedNumber))
                {
                    numberOfRecords = parsedNumber;
                }

                // Consultar la base de datos
                string recordsMessage = await GetRecordsFromDatabase(numberOfRecords);
                // Actualiza el historial de mensajes
                var updatedMessages = messages.Concat(new[]
                {
                    new
                    {
                        role = "user",
                        content = userMessage
                    },
                    new
                    {
                        role = "assistant",
                        content = recordsMessage
                    }
                }).ToArray();

                // Muestra los resultados
                Console.WriteLine($"Chatbot: {recordsMessage}");
                continue;
            }

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
            string botReply = responseObject.choices[0].message.content;

            // Muestra la respuesta del chatbot
            Console.WriteLine($"Chatbot: {botReply}");

            // Actualiza el historial de mensajes
            messages = conversation.Concat(new[]
            {
                new
                {
                    role = "assistant",
                    content = botReply
                }
            }).ToArray();
        }
    }

    static async Task<string> GetRecordsFromDatabase(int numberOfRecords)
    {
        string connectionString = "Server=tcp:aplix-bd-desarrollo.database.windows.net,1433;Initial Catalog=WB_APLIX;Persist Security Info=False;User ID=admin-db;Password=Sql2016!!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"; // Reemplaza con tu cadena de conexión

        // Ajusta la consulta SQL para seleccionar el número de registros
        string query = "SELECT TOP (@numberOfRecords) * FROM Usuarios.Usuario";

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@numberOfRecords", numberOfRecords);

            connection.Open();

            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                var records = new StringBuilder();

                while (await reader.ReadAsync())
                {
                    // Ajusta según las columnas de tu tabla
                    records.AppendLine($"{reader["Nombre"]} - {reader["Usuario"]}");
                }

                return records.ToString();
            }
        }
    }
}
