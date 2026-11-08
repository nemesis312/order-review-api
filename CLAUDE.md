# CLAUDE.md

API de órdenes en .NET 10 (Controllers, EF Core 10, SQLite, NUnit + Moq). Código en inglés, documentación en español. Baseline educativo: sé conservador y mantén el diff pequeño.

## Comandos

```bash
dotnet build
dotnet test
dotnet run --project src/OrderReview.Api --launch-profile http   # Development, http://localhost:5010
dotnet ef database update --project src/OrderReview.Api
dotnet ef migrations add <Nombre> --project src/OrderReview.Api --output-dir Data/Migrations
```

No uses `--no-launch-profile`: el entorno pasa a Production (sin seeding, sin Swagger, sin config de `user-jwts`).

## Arquitectura

`Controller -> Service -> Repository -> DbContext`. Sin MediatR, CQRS, AutoMapper, Generic Repository ni Unit of Work. No agregues dependencias ni abstracciones sin propósito.

| Capa | Archivos | Responsabilidad |
|---|---|---|
| Controller | `Controllers/OrdersController.cs` | HTTP, DTOs, `[Authorize(Policy = ...)]`, `CancellationToken`. Delega; sin reglas de negocio. |
| Service | `Services/OrderService.cs` | Validaciones y reglas de negocio. Mapea entidad a DTO (`OrderResponse.From`). Lanza excepciones de dominio. |
| Repository | `Repositories/OrderRepository.cs` | Consultas y persistencia EF Core. Sin reglas de negocio. |
| DbContext | `Data/AppDbContext.cs` | Mapeo y Fluent API. Migraciones en `Data/Migrations/`. |

No dupliques reglas entre capas. Nunca devuelvas entidades EF: usa DTOs.

## Patrones existentes a reutilizar

- **Validación:** solo en `OrderService`; junta errores por campo y lanza `OrderValidationException`. `Errors/ApiExceptionHandler.cs` la convierte en 400. Los DTOs no llevan data annotations a propósito (`Description` es `string?` para evitar el `[Required]` implícito de MVC).
- **No encontrado:** `OrderNotFoundException` desde el servicio; el handler responde 404.
- **Errores:** cualquier excepción nueva se mapea en `ApiExceptionHandler`, el único lugar que las traduce. Lo no previsto es un 500 genérico sin detalles internos.
- **Autorización:** constantes en `Security/AuthorizationPolicies.cs` (`OrdersRead`, `OrdersCreate`, `ReadScope`, `CreateScope`) y `AddOrderPolicies`. No escribas strings sueltos.
- **Tiempo:** inyecta `TimeProvider` (registrado en `Program.cs`); no uses `DateTimeOffset.UtcNow` directo.
- **Mapeo a DTO:** en el servicio, con el método estático `OrderResponse.From`.
- **Pruebas de integración:** `tests/OrderReview.Tests/Support/OrderReviewApiFactory.cs` (SQLite en memoria aislado, entorno `Testing`, tokens firmados en proceso con `CreateClientWithScopes` / `CreateToken`, helpers `SeedOrderAsync`, `FindOrderAsync`, `CountOrdersAsync`).

## Reglas de SQLite

- `DateTimeOffset` se guarda como ticks UTC (convención en `AppDbContext`); así funciona `ORDER BY`.
- `decimal` se guarda como TEXT: nunca ordenes ni filtres por `Total` en SQL.
- `OrderResponse` omite `CancellationReason` y `CancelledAt` a propósito.

## Pruebas

- Toda funcionalidad nueva lleva pruebas: unitarias (NUnit + Moq, `tests/OrderReview.Tests/Services/`) para reglas y llamadas al repositorio; integración (`Controllers/`) para códigos HTTP y autorización.
- Cubre los caminos de error (400, 401, 403, 404, 409 si aplica), no solo el camino feliz.
- Las assertions deben verificar comportamiento (estado resultante, qué se persistió, qué no se llamó), no solo "no lanza".
- Ejecuta `dotnet test` y reporta el resultado real.

## Seguridad

- Cada endpoint nuevo necesita su **propia policy** (no reutilices `Orders.Read` u `Orders.Create` para otra acción) y su scope en `AuthorizationPolicies`.
- Esta es una API interna de back-office (staff): el claim `scope` es el control de acceso y **no hay comprobación de propiedad por cliente** (cualquier token con `orders:read` ve todas las órdenes; `CustomerId` viene del body al crear). Si algún día se introduce propiedad por orden/cliente, debe diseñarse explícitamente, no asumirse.
- No agregues autenticación falsa ni secretos al repo. Los tokens de desarrollo salen de `dotnet user-jwts` (llave en user-secrets).
- No expongas excepciones ni detalles internos en las respuestas.

## Alcance y git

- Inspecciona el repo antes de empezar. No modifiques archivos ajenos a la tarea sin justificarlo.
- No hagas commits, merges ni push automáticamente. Deja el diff listo para revisión humana.
- Este baseline no tiene endpoint de cancelación; solo se agrega cuando se pide en `feature/cancel-orders-ai`.
