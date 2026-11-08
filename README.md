# OrderReview API

API web en .NET 10 con tres endpoints de órdenes (listar, consultar por id, crear). Es el **baseline** del video de BoricuaCoder "Reviso en vivo código que escribió la IA".

## Objetivo educativo

Tener una base pequeña, con convenciones claras, para pedirle una funcionalidad real a una IA (cancelar órdenes) y luego revisar el diff con un checklist, sin confiar a ciegas en el resultado. Ver [docs/REVIEW-CHECKLIST.md](docs/REVIEW-CHECKLIST.md) y [docs/DEMO-GUIDE.md](docs/DEMO-GUIDE.md).

> **Este repo no tiene endpoint de cancelación a propósito.** El modelo `Order` ya trae `CancellationReason` y `CancelledAt`, y el enum tiene `Cancelled`, pero no existe ninguna operación que los use. La cancelación se implementa después, en la rama `feature/cancel-orders-ai` (ver [docs/FEATURE-PROMPT.md](docs/FEATURE-PROMPT.md)).

> **Advertencia: es una demo, no es código de producción.** Los tokens se generan con `dotnet user-jwts` (solo desarrollo), la base de datos es un archivo SQLite local y los datos de ejemplo son ficticios. No lo despliegues tal cual.

## Requisitos

- .NET 10 SDK
- Herramienta `dotnet-ef` (para crear/actualizar la base de datos a mano):

```bash
dotnet tool install --global dotnet-ef
```

Con `dotnet-ef` 10.0.5 y runtime 10.0.14 verás un aviso de versión; es inofensivo.

## Restaurar, compilar y ejecutar

Desde la raíz del repo:

```bash
dotnet restore
dotnet build
dotnet run --project src/OrderReview.Api --launch-profile http
```

El perfil `http` (`src/OrderReview.Api/Properties/launchSettings.json`) levanta en `http://localhost:5010` con `ASPNETCORE_ENVIRONMENT=Development`. El perfil `https` usa `https://localhost:7070`.

> **Cuidado con `--no-launch-profile`.** Sin perfil el entorno es Production: no se crea la base de datos, no se cargan datos de ejemplo, no hay Swagger y falta la configuración de autenticación que usa `dotnet user-jwts` (`appsettings.Development.json`). Usa siempre `--launch-profile http`.

Swagger UI (solo en Development): `http://localhost:5010/swagger`. Tiene esquema Bearer: pega el token sin el prefijo `Bearer `.

## Base de datos

SQLite, archivo `src/OrderReview.Api/order-review.db` (ignorado por git). La cadena de conexión está en `src/OrderReview.Api/appsettings.json`.

- **Crear/actualizar:** en Development, `Program.cs` ejecuta `MigrateAsync` y el seeder al arrancar. También puedes hacerlo a mano:

```bash
dotnet ef database update --project src/OrderReview.Api
```

- **Reiniciar:** borra `src/OrderReview.Api/order-review.db` y vuelve a ejecutar la app.
- **Datos de ejemplo (solo Development, idempotente):** 5 órdenes con IDs fijos.

| Id | Descripción | Total | Estado |
|---|---|---|---|
| `a0000000-0000-0000-0000-000000000001` | Mechanical keyboard | 129.99 | Pending |
| `a0000000-0000-0000-0000-000000000002` | USB-C docking station | 249.50 | Pending |
| `a0000000-0000-0000-0000-000000000003` | 27-inch monitor | 389.00 | Processing |
| `a0000000-0000-0000-0000-000000000004` | Noise-cancelling headphones | 199.00 | Shipped |
| `a0000000-0000-0000-0000-000000000005` | Laptop stand | 45.25 | Delivered |

Clientes de ejemplo: `11111111-1111-1111-1111-111111111111` y `22222222-2222-2222-2222-222222222222`.

Detalles de SQLite que importan al revisar código: `DateTimeOffset` se guarda como ticks UTC (INTEGER) para poder ordenar por fecha; `decimal` se guarda como TEXT, así que **no se debe ordenar ni filtrar por `Total` en SQL**.

## Autenticación (tokens de desarrollo)

JWT Bearer. Los tokens se generan con `dotnet user-jwts` desde `src/OrderReview.Api`; la llave de firma queda en user-secrets, fuera del repo. El claim `scope` decide el acceso.

Esta es una API **interna de back-office (personal/staff)**: el scope es el único control de acceso. No hay propiedad por cliente: cualquier token con `orders:read` ve todas las órdenes, y `CustomerId` viene en el body al crear.

| Policy | Scope requerido | Endpoints |
|---|---|---|
| `Orders.Read` | `orders:read` | `GET /api/orders`, `GET /api/orders/{id}` |
| `Orders.Create` | `orders:create` | `POST /api/orders` |

```bash
cd src/OrderReview.Api

# Solo lectura
dotnet user-jwts create --name reader --scope orders:read

# Lectura y creación
dotnet user-jwts create --name creator --scope orders:read --scope orders:create

# Imprimir solo el token (para usar en una variable)
dotnet user-jwts create --scope orders:read -o token

dotnet user-jwts list
dotnet user-jwts print <id>
dotnet user-jwts clear
```

Ojo: una policy no implica a la otra. Un token que solo tiene `orders:create` no puede hacer GET.

## Pruebas

```bash
dotnet test
```

- Unitarias (NUnit + Moq): `tests/OrderReview.Tests/Services/`
- Integración (`WebApplicationFactory`, SQLite en memoria aislado, tokens firmados en proceso): `tests/OrderReview.Tests/Controllers/`, con la fábrica en `tests/OrderReview.Tests/Support/OrderReviewApiFactory.cs`

En el baseline son 47 pruebas; el número cambia en cuanto se agregue código.

## Ejemplos HTTP

Genera un token y guárdalo (desde `src/OrderReview.Api`) con la app corriendo:

```bash
TOKEN=$(dotnet user-jwts create --name creator --scope orders:read --scope orders:create -o token)
```

Listar (200):

```bash
curl -s http://localhost:5010/api/orders -H "Authorization: Bearer $TOKEN"
```

Consultar por id (200):

```bash
curl -s http://localhost:5010/api/orders/a0000000-0000-0000-0000-000000000001 \
  -H "Authorization: Bearer $TOKEN"
```

Crear (201 con header `Location`):

```bash
curl -i -X POST http://localhost:5010/api/orders \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","description":"Webcam HD","total":79.90}'
```

Sin token (401):

```bash
curl -i http://localhost:5010/api/orders
```

Token sin el scope necesario (403): crea un token de solo lectura e intenta crear.

```bash
READER=$(dotnet user-jwts create --name reader --scope orders:read -o token)
curl -i -X POST http://localhost:5010/api/orders \
  -H "Authorization: Bearer $READER" -H "Content-Type: application/json" \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","description":"Webcam HD","total":79.90}'
```

Datos inválidos (400 con `errors` por campo: `customerId`, `total`, `description`):

```bash
curl -i -X POST http://localhost:5010/api/orders \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"customerId":"00000000-0000-0000-0000-000000000000","description":"","total":0}'
```

Orden inexistente (404):

```bash
curl -i http://localhost:5010/api/orders/a0000000-0000-0000-0000-0000000000ff \
  -H "Authorization: Bearer $TOKEN"
```

Errores no previstos devuelven un 500 genérico (`ProblemDetails`) sin detalles internos.

## Reglas de negocio

- La orden nueva queda en `Pending`.
- `Total` > 0, con máximo 2 decimales y máximo 1,000,000 (error bajo la clave `total`). `CustomerId` distinto de `Guid.Empty`.
- `Description` obligatoria (se recorta con `Trim`), máximo 250 caracteres.
- `OrderResponse` no expone `CancellationReason` ni `CancelledAt`.

## Arquitectura

```text
HTTP
 |
 v
OrdersController      HTTP, DTOs, autorización por policy; delega
 |
 v
OrderService          validaciones y reglas de negocio; mapea a DTO
 |
 v
OrderRepository       consultas y persistencia con EF Core
 |
 v
AppDbContext          mapeo y configuración (Fluent API) -> SQLite
```

Los errores se traducen en un solo lugar: `Errors/ApiExceptionHandler.cs`. `OrderValidationException` produce 400 (`ValidationProblemDetails`), `OrderNotFoundException` produce 404 y cualquier otra excepción produce un 500 genérico. Las convenciones para trabajar en el repo están en [CLAUDE.md](CLAUDE.md).
