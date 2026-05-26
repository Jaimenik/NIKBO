using NikSBO.Exceptions;
using NikSBO.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NikSBO.http
{
    public partial class B1Client
    {
        /// <summary>
        /// Crea una tabla de usuario (UDT) si no existe. Idempotente: si ya existe en SAP,
        /// no hace nada. Útil para installers de addon que se ejecutan en cada arranque.
        /// </summary>
        /// <param name="definition">Definición de la UDT a crear.</param>
        /// <param name="cancellationToken">Token para cancelar la operación.</param>
        public async Task EnsureUserTableAsync(
        UserTableDefinition definition,
        CancellationToken cancellationToken = default)
        {
            try
            {
                await GetByEndpointAsync<object>(
                    $"UserTablesMD('{definition.TableName}')",
                    cancellationToken);

                Log($"UserTable '{definition.TableName}' ya existe, skip");
                return;
            }
            catch (B1Exception ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
               
            }

            Log($"Creando UserTable '{definition.TableName}'...");
            await PostByEndpointAsync<object>("UserTablesMD", definition, cancellationToken);
            Log($"UserTable '{definition.TableName}' creada");
        }

        public async Task EnsureUserFieldAsync(
     UserFieldDefinition definition,
     CancellationToken cancellationToken = default)
        {
            // 1) Chequear existencia con $filter
            var filterClause = $"TableName eq '{definition.TableName}' and Name eq '{definition.Name}'";
            var endpoint = $"UserFieldsMD?$filter={Uri.EscapeDataString(filterClause)}";

            var response = await GetByEndpointAsync<JsonElement>(endpoint, cancellationToken);
            var exists = response.GetProperty("value").GetArrayLength() > 0;

            if (exists)
            {
                Log($"UserField '{definition.TableName}.U_{definition.Name}' ya existe, skip");
                return;
            }

            // 2) Crear con POST
            Log($"Creando UserField '{definition.TableName}.U_{definition.Name}'...");
            await PostByEndpointAsync<object>("UserFieldsMD", definition, cancellationToken);
            Log($"UserField '{definition.TableName}.U_{definition.Name}' creado");
        }

        /// <summary>
        /// Crea un objeto de usuario (UDO) si no existe. Idempotente: si ya existe en SAP,
        /// no hace nada. Útil para installers de addon que se ejecutan en cada arranque.
        /// El UDO se monta sobre una UDT existente — asegúrate de haberla creado antes
        /// con <see cref="EnsureUserTableAsync"/>.
        /// </summary>
        /// <param name="definition">Definición del UDO a crear.</param>
        /// <param name="cancellationToken">Token para cancelar la operación.</param>
        public async Task EnsureUserObjectAsync(
            UserObjectDefinition definition,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await GetByEndpointAsync<object>(
                    $"UserObjectsMD('{definition.Code}')",
                    cancellationToken);

                Log($"UserObject '{definition.Code}' ya existe, skip");
                return;
            }
            catch (B1Exception ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {

            }

            Log($"Creando UserObject '{definition.Code}'...");
            await PostByEndpointAsync<object>("UserObjectsMD", definition, cancellationToken);
            Log($"UserObject '{definition.Code}' creado");
        }
    }
}
