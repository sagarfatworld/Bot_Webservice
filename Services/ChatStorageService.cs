using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using Botatwork_in_Livechat.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Botatwork_in_Livechat.Services
{
    public interface IChatStorageService
    {
        Task StoreMessage(string chatId, string visitorMessage, string botResponse, string agentEmail);
        Task UpdateCopyStatus(string messageHash);
        Task<List<ChatMessage>> GetChatMessages(string chatId);
    }

    public class ChatStorageService : IChatStorageService
    {
        private readonly string _connectionString;
        private readonly ILogger<ChatStorageService> _logger;

        public ChatStorageService(IConfiguration configuration, ILogger<ChatStorageService> logger)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _logger = logger;

            // Test connection synchronously in constructor
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    _logger.LogInformation("Database connection test successful");
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError($"Database connection failed: {ex.Message}, Error Number: {ex.Number}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Database connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task StoreMessage(string chatId, string visitorMessage, string botResponse, string agentEmail)
        {
            try
            {
                _logger.LogInformation($"Storing message - ChatId: {chatId}, AgentEmail: {agentEmail}");

                var messageHash = GenerateMessageHash(chatId, visitorMessage, botResponse);

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        // Check if message exists
                        var checkCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM ChatMessages WHERE MessageHash = @MessageHash",
                            connection, transaction);
                        checkCmd.Parameters.AddWithValue("@MessageHash", messageHash);

                        var exists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                        if (!exists)
                        {
                            var cmd = new SqlCommand(@"
                                INSERT INTO ChatMessages 
                                    (ChatId, VisitorMessage, BotResponse, AgentEmail, MessageHash, Timestamp) 
                                VALUES 
                                    (@ChatId, @VisitorMessage, @BotResponse, @AgentEmail, @MessageHash, GETUTCDATE())",
                                connection, transaction);

                            cmd.Parameters.AddWithValue("@ChatId", chatId);
                            cmd.Parameters.AddWithValue("@VisitorMessage", (object)visitorMessage ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BotResponse", (object)botResponse ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@AgentEmail", agentEmail);
                            cmd.Parameters.AddWithValue("@MessageHash", messageHash);

                            var result = await cmd.ExecuteNonQueryAsync();
                            await transaction.CommitAsync();
                            _logger.LogInformation($"Message stored successfully. Rows affected: {result}");
                        }
                        else
                        {
                            await transaction.CommitAsync();
                            _logger.LogInformation("Message already exists, skipping insertion");
                        }
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError($"SQL Error storing message: {ex.Message}, Number: {ex.Number}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error storing message: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateCopyStatus(string messageHash)
        {
            try
            {
                _logger.LogInformation($"Updating copy status for hash: {messageHash}");

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        var cmd = new SqlCommand(@"
                            UPDATE ChatMessages 
                            SET CopyStatus = ISNULL(CopyStatus, 0) + 1 
                            WHERE MessageHash = @MessageHash",
                            connection, transaction);

                        cmd.Parameters.AddWithValue("@MessageHash", messageHash);
                        var result = await cmd.ExecuteNonQueryAsync();

                        await transaction.CommitAsync();
                        _logger.LogInformation($"Copy status updated. Rows affected: {result}");
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError($"SQL Error updating copy status: {ex.Message}, Number: {ex.Number}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating copy status: {ex.Message}");
                throw;
            }
        }

        public async Task<List<ChatMessage>> GetChatMessages(string chatId)
        {
            var messages = new List<ChatMessage>();

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var cmd = new SqlCommand(@"
                        SELECT 
                            Id, ChatId, VisitorMessage, BotResponse, 
                            AgentEmail, CopyStatus, Timestamp, MessageHash 
                        FROM ChatMessages 
                        WHERE ChatId = @ChatId 
                        ORDER BY Timestamp",
                        connection);

                    cmd.Parameters.AddWithValue("@ChatId", chatId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            messages.Add(new ChatMessage
                            {
                                Id = reader.GetInt32(0),
                                ChatId = reader.GetString(1),
                                VisitorMessage = reader.IsDBNull(2) ? null : reader.GetString(2),
                                BotResponse = reader.IsDBNull(3) ? null : reader.GetString(3),
                                AgentEmail = reader.GetString(4),
                                CopyStatus = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                Timestamp = reader.GetDateTime(6),
                                MessageHash = reader.GetString(7)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting chat messages: {ex.Message}");
                throw;
            }

            return messages;
        }

        private string GenerateMessageHash(string chatId, string visitorMessage, string botResponse)
        {
            using (var sha256 = SHA256.Create())
            {
                var combinedMessage = $"{chatId}|{visitorMessage}|{botResponse}";
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedMessage));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}