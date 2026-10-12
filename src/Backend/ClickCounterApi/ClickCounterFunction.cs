using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Net;

namespace ClickCounterApi
{
    public class ClickCounterFunction
    {
        private readonly ILogger<ClickCounterFunction> _logger;

        public ClickCounterFunction(ILogger<ClickCounterFunction> logger)
        {
            _logger = logger;
        }

        [Function("ClickCounter")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("Processando requisição do contador de cliques.");

            int currentClicks = 0;
            string connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("A string de conexão 'SqlConnectionString' não está configurada.");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Erro de configuração no servidor.");
                return errorResponse;
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Garante que a tabela existe
                    string createTableQuery = @"
                        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Counters' and xtype='U')
                        CREATE TABLE Counters (
                            Id INT PRIMARY KEY,
                            ClickCount INT NOT NULL
                        );
                        IF NOT EXISTS (SELECT * FROM Counters WHERE Id = 1)
                        INSERT INTO Counters (Id, ClickCount) VALUES (1, 0);";

                    using (SqlCommand createCmd = new SqlCommand(createTableQuery, connection))
                    {
                        await createCmd.ExecuteNonQueryAsync();
                    }

                    // Se for POST, incrementa o contador
                    if (req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        string updateQuery = "UPDATE Counters SET ClickCount = ClickCount + 1 WHERE Id = 1;";
                        using (SqlCommand updateCmd = new SqlCommand(updateQuery, connection))
                        {
                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Lê o valor atual
                    string selectQuery = "SELECT ClickCount FROM Counters WHERE Id = 1;";
                    using (SqlCommand selectCmd = new SqlCommand(selectQuery, connection))
                    {
                        var result = await selectCmd.ExecuteScalarAsync();
                        if (result != null)
                        {
                            currentClicks = Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro ao acessar o banco de dados: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Erro no banco de dados: {ex.Message}");
                return errorResponse;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { count = currentClicks });
            
            return response;
        }
    }
}