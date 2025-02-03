using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;
using ToDoAppShared;

namespace ToDoAppServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);

            // Add CORS services
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.WithOrigins("http://localhost:64225")   // client URL
                           .AllowAnyHeader()
                           .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            // Use CORS middleware
            app.UseCors();

            var connectionString = builder.Configuration.GetConnectionString("SQLiteConnection");

            try
            {   
                // iniciace datab�ze
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();   // automaticky vytvo�� z parametr� p�ipojen� datab�zov� soubor SQLite, pokud je�t� neexistuje.    
                    using (var command = connection.CreateCommand())
                    {
                        // obnov p��padn� neexistuj�c� tabulku TodoItems
                        command.CommandText = $@"CREATE TABLE IF NOT EXISTS TodoItems(
                                                     Id TEXT PRIMARY KEY,
                                                     Title TEXT NOT NULL,
                                                     State INTEGER NOT NULL,
                                                     Content TEXT,
                                                     CHECK (length(Title) <= 255),
                                                     CHECK (State >= 1 AND State <= 3),
                                                     CHECK (length(Content) <= 2000));".Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
                        await command.ExecuteNonQueryAsync();

                        // napl� tabulku TodoItems testovac�mi daty, pokud je pr�zdn�
                        command.CommandText = "SELECT COUNT(*) FROM TodoItems";
                        var count = (long)(await command.ExecuteScalarAsync() ?? 0);
                        if (count == 0)
                        {
                            command.CommandText = $@"INSERT INTO TodoItems (Id, Title, State, Content) VALUES ('{Guid.NewGuid()}', '�kol 1', 1, 'Naprogramovat svou prvn� webovou aplikaci, serverovou ��st v .Netu, ulo�i�t� na SQLite a klienta na Vue3 dle zad�n�.');
                                                     INSERT INTO TodoItems (Id, Title, State, Content) VALUES ('{Guid.NewGuid()}', 'Task 2', 2, 'Content for in-progress Task 2');
                                                     INSERT INTO TodoItems (Id, Title, State, Content) VALUES ('{Guid.NewGuid()}', 'Task 3', 3, 'Content for finished Task 3');
                                                     INSERT INTO TodoItems (Id, Title, State, Content) VALUES ('{Guid.NewGuid()}', 'Task 4', 2, 'Content for in-progress Task 4');
                                                     INSERT INTO TodoItems (Id, Title, State, Content) VALUES ('{Guid.NewGuid()}', 'Task 5', 3, 'Content for finished Task 5');
                                                     ".Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
                        await command.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch // (Exception ex)
            {
                // chyba p�i selh�v�n� create dotazu, duplicit� z�znam� apod.
                // ukon�en� iniciace aplikace a vyps�n� chyby do protokolu
            }


            app.MapGet("/todo", async (HttpContext context) =>
            {
                try
                {
                    string? queryString = context.Request.QueryString.ToString();
                    if (!string.IsNullOrEmpty(queryString) && (!queryString.StartsWith("?state=")))
                    {
                        return Results.BadRequest(new { isError = true, error = new { code = "INVALID_STATE_PARAMETER", message = $"Invalid state parameter: ?state= ({queryString})." } });
                    }

                    string? stateParam = context.Request.Query["state"].ToString();
                    var validStates = new List<string> { "all", "created", "finished" };
                    if (!string.IsNullOrEmpty(stateParam) && !validStates.Contains(stateParam))
                    {
                        return Results.BadRequest(new { isError = true, error = new { code = "INVALID_STATE_VALUE", message = $"Invalid state value: created|finished|all ({stateParam})." } });
                    }

                    using var connection = new SqliteConnection(connectionString);
                    await connection.OpenAsync();

                    if (!await new DatabaseHelper().ExistsTable("TodoItems", connection))
                    {
                        return Results.NotFound(new { isError = true, error = new { code = "404", message = "Table 'TodoItems' does not exist." } });
                    }

                    // parametr ?state=created|finished|all zad�n jinak ne� je strukturovan� StateEnum, bude nutn� si vyjasnit zad�n�, nap��klad StateEnum.InProgress chyb�        
                    var command = connection.CreateCommand();
                    if (string.IsNullOrEmpty(stateParam) || stateParam == "all")
                    {
                        command.CommandText = "SELECT * FROM TodoItems";
                    }
                    else if (stateParam == "created")   // za p�edpokladu, �e created odpov�d� ToDoAppShared.StateEnum.Open
                    {
                        command.CommandText = $"SELECT * FROM TodoItems WHERE State = {(int)ToDoAppShared.StateEnum.Open}";
                    }
                    else if (stateParam == "finished")
                    {
                        command.CommandText = $"SELECT * FROM TodoItems WHERE State = {(int)ToDoAppShared.StateEnum.Finished}";
                    }
                    else
                    {
                        // mrtv� v�tev
                        return Results.BadRequest(new { isError = true, error = new { code = "INVALID_STATE", message = "Invalid state parameter." } });
                    }

                    var items = new List<ToDoItem>();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            items.Add(new ToDoItem
                            {
                                Id = reader.GetGuid(0),
                                Title = reader.GetString(1),
                                State = Enum.TryParse<ToDoAppShared.StateEnum>(reader.GetString(2), out var state) ? state : ToDoAppShared.StateEnum.Open,
                                Content = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                            });
                        }
                    }

                    // if (items.Count == 0) return Results.NoContent();  // mo�n� zp�sob �e�en� pr�zdn� kolekce
                    return Results.Ok(items);
                }
                catch (SqliteException ex)
                {
                    return Results.NotFound(new { isError = true, error = new { code = "404", message = "Items not found. " + ex.Message } });
                }
                catch (Exception ex)
                {
                    return Results.NotFound(new { isError = true, error = new { code = "404", message = "Items not found. " + ex.Message } });
                }
            });


            app.MapGet("/todo/{id}", async (string id) =>
            {
                try
                {
                    //zde lze p��padn� �e�it validaci guidu (d�lku �et�zce a jeho strukturu p�es regul�rn� v�raz)

                    using var connection = new SqliteConnection(connectionString);
                    await connection.OpenAsync();

                    if (!await new DatabaseHelper().ExistsTable("TodoItems", connection))
                    {
                        return Results.NotFound(new { isError = true, error = new { code = "404", message = "Table 'TodoItems' does not exist." } });
                    }

                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM TodoItems WHERE Id = @Id";
                    command.Parameters.AddWithValue("@Id", id);
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var item = new ToDoItem
                        {
                            Id = reader.GetGuid(0),
                            Title = reader.GetString(1),
                            State = Enum.TryParse<ToDoAppShared.StateEnum>(reader.GetString(2), out var state) ? state : ToDoAppShared.StateEnum.Open,
                            Content = reader.IsDBNull(3) ? String.Empty : reader.GetString(3)
                        };
                        return Results.Ok(item);
                    }
                    else
                    {
                        return Results.NotFound(new { isError = true, error = new { code = "404", message = "Item not found." } });
                    }
                }
                catch (SqliteException ex)
                {
                    return Results.NotFound(new { isError = true, error = new { code = "404", message = "Item not found. " + ex.Message } });
                }
                catch (Exception ex)
                {
                    return Results.NotFound(new { isError = true, error = new { code = "404", message = "Item not found. " + ex.Message } });
                }
            });


            app.MapPost("/todo", async (ToDoItem item) =>
            {
                try
                {
                    if (item == null)
                    {
                        return Results.BadRequest(new { isError = true, error = new { code = "400", message = "Item is null." } });
                    }

                    item.Id = Guid.NewGuid(); // Generate UUIDv4

                    using var connection = new SqliteConnection(connectionString);
                    await connection.OpenAsync();

                    var command = connection.CreateCommand();
                    command.CommandText = "INSERT INTO TodoItems (Id, Title, State, Content) VALUES (@Id, @Title, @State, @Content)";
                    command.Parameters.AddWithValue("@Id", item.Id.ToString().ToLower());  // Convert GUID to lowercase
                    command.Parameters.AddWithValue("@Title", item.Title);
                    command.Parameters.AddWithValue("@State", item.State);
                    command.Parameters.AddWithValue("@Content", item.Content);

                    var result = await command.ExecuteNonQueryAsync();
                    if (result > 0)
                    {
                        return Results.Created($"/todo/{item.Id}", item);
                    }
                    else
                    {
                        return Results.BadRequest(new { isError = true, error = new { code = "400", message = "Failed to insert the item." } });
                    }
                }
                catch (SqliteException ex)
                {
                    return Results.InternalServerError(new { isError = true, error = new { code = "500", message = ex.Message } });
                }
                catch (Exception ex)
                {
                    return Results.InternalServerError(new { isError = true, error = new { code = "500", message = ex.Message } });
                }
            });


            app.MapPut("/todo/{id}", async (string id, ToDoItem item) =>
            {
                try
                {
                    if (item == null)
                    {
                        return Results.BadRequest(new { isError = true, error = new { code = "400", message = "Item is null." } });
                    }

                    using var connection = new SqliteConnection(connectionString);
                    await connection.OpenAsync();

                    var command = connection.CreateCommand();
                    command.CommandText = "UPDATE TodoItems SET Title = @Title, State = @State, Content = @Content WHERE Id = @Id";
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@Title", item.Title);
                    command.Parameters.AddWithValue("@State", item.State);
                    command.Parameters.AddWithValue("@Content", item.Content);

                    var result = await command.ExecuteNonQueryAsync();
                    if (result > 0)
                    {
                        return Results.Ok(item);
                    }

                    return Results.NotFound(new { isError = true, error = new { code = "404", message = "Item not found." } });
                }
                catch (SqliteException ex)
                {
                    return Results.InternalServerError(new { isError = true, error = new { code = "500", message = ex.Message } });
                }
                catch (Exception ex)
                {
                    return Results.InternalServerError(new { isError = true, error = new { code = "500", message = ex.Message } });
                }
            });

            app.MapDelete("/todo/{id}", async (string id) =>
            {
                try
                {
                    using var connection = new SqliteConnection(connectionString);
                    await connection.OpenAsync();

                    var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM TodoItems WHERE Id = @Id";
                    command.Parameters.AddWithValue("@Id", id);

                    var result = await command.ExecuteNonQueryAsync();
                    if (result > 0)
                    {
                        return Results.Accepted();
                    }
                    return Results.NoContent();
                }
                catch (SqliteException ex)
                {
                    return Results.InternalServerError(new { isError = true, error = new { code = "500", message = ex.Message } });
                }
                catch (Exception ex)
                {
                    return Results.InternalServerError(new { isError = true, error = new { code = "500", message = ex.Message } });
                }
            });


            app.MapFallback((HttpContext context) =>
            {
                return Results.BadRequest(new { isError = true, error = new { code = "400", message = "Endpoint not found." } });
            });

            app.Run();
        }
    }
    
    public class ToDoItem
    {
        [Key]
        [RegularExpression(@"^[a-f\d]{8}(-[a-f\d]{4}){3}-[a-f\d]{12}$", ErrorMessage = "Id �lohy nen� ve spr�vn�m form�tu.")]
        public Guid Id { get; set; } = Guid.NewGuid(); // UUID4
    
        [Required (ErrorMessage = "Nen� zad�n n�zev �lohy.")]
        [MaxLength(255, ErrorMessage = "N�zev �lohy je p��li� dlouh�.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nen� zad�n stav �lohy.")]
        public StateEnum State { get; set; } = StateEnum.Open;
    
        [MaxLength(2000,ErrorMessage ="Popis �lohy je p��li� dlouh�.")]
        public string Content { get; set; } = string.Empty;

        //public enum StateEnum
        //{
        //    Open = 1,
        //    InProgress = 2,
        //    Finished = 3
        //}
    }
    
    public class DatabaseHelper
    {
        public async Task<bool> ExistsTable(string tableName, SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            { 
                command.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'";
                var tableExists = await command.ExecuteScalarAsync();
                if (tableExists == null) return false;
                return true;
            }
        }
    }
}




