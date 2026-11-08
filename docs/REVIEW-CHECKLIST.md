# Checklist de revisión de código escrito por IA

Las 8 preguntas, con qué mirar en este repo. Responde con evidencia del diff y de las pruebas, no con impresiones.

## 1. ¿Hizo exactamente lo que pedí?

- Compara el diff con cada requisito del prompt (`docs/FEATURE-PROMPT.md`): endpoint, autorización, estados no cancelables, razón obligatoria, errores, pruebas.
- Busca lo que se agregó y nadie pidió (endpoints extra, campos nuevos en `OrderResponse`, cambios de comportamiento en GET/POST existentes).
- Busca lo que faltó: ¿cada regla del prompt tiene código y una prueba?

## 2. ¿Qué archivos modificó y por qué?

- Corre `git diff main...feature/cancel-orders-ai --stat` y justifica cada archivo.
- Sospecha de cambios en `Program.cs`, `AppDbContext.cs`, `ApiExceptionHandler.cs`, `DbSeeder.cs` o `appsettings*.json`: son archivos compartidos.
- Si hay migración nueva, verifica que el modelo realmente lo necesitaba (`CancellationReason` y `CancelledAt` ya existen).

## 3. ¿Qué ocurre cuando algo falla?

- ¿La orden inexistente da 404 vía `OrderNotFoundException` y no un `null` o un 500?
- ¿Las reglas de negocio violadas (razón vacía, estado no cancelable) producen un código y cuerpo claros, pasando por `ApiExceptionHandler`?
- ¿Algún mensaje filtra detalles internos o stack traces?
- ¿Qué pasa con dos cancelaciones seguidas de la misma orden? ¿Y con una cancelación concurrente?

## 4. ¿Quién puede ejecutar esto y qué datos toca?

- ¿El endpoint tiene su propia policy en `Security/AuthorizationPolicies.cs` o reutiliza `Orders.Read`/`Orders.Create`?
- ¿Se comprueba que el usuario tiene acceso a **esa** orden, o basta con tener el scope? (El token solo lleva `scope`; mira cómo se relaciona con `CustomerId`.)
- Nota: el baseline **deliberadamente no tiene `FallbackPolicy`**, así que un endpoint sin `[Authorize]` es anónimo. Verifica que cada acción nueva tenga `[Authorize(Policy = ...)]`.
- ¿Qué columnas escribe? Revisa que no toque `Total`, `CustomerId` ni `Description`, y que `UpdatedAt` se llene con `TimeProvider`.

## 5. ¿Respeta los patrones existentes?

- Capas: ¿el controller solo delega, las reglas están en `OrderService` y el repository solo persiste?
- ¿Usa `OrderValidationException` / `OrderNotFoundException` y las constantes de `AuthorizationPolicies` en vez de inventar equivalentes?
- ¿Inyecta `TimeProvider` y recibe `CancellationToken` como el resto del código?

## 6. ¿Está duplicando funcionalidad?

- ¿Repite la validación en controller y service?
- ¿Creó un helper, excepción o mapeo que ya existía (`OrderResponse.From`, `OrderNotFoundException`)?
- ¿Agregó su propia consulta cuando `IOrderRepository.GetByIdAsync` ya la resolvía?

## 7. ¿Las pruebas realmente verifican el comportamiento?

- ¿Las assertions comprueban el estado persistido (`FindOrderAsync`) y no solo el status code?
- ¿Hay pruebas para cada estado no cancelable (Shipped, Delivered, Cancelled), razón vacía, 404, 401 y 403?
- ¿Los mocks de Moq verifican llamadas relevantes, o la prueba pasaría igual con el código roto? Prueba rompiendo el código a propósito para ver si la prueba falla.
- Corre `dotnet test` tú mismo; no aceptes un "pasaron" sin ver la salida.

## 8. ¿Puedo explicar el código?

- Lee el diff línea por línea. Si no puedes explicar una línea, pregunta o elimínala.
- ¿Puedes explicar cada decisión de autorización y de manejo de errores sin mirar el código?
- ¿Hay nombres, comentarios o abstracciones que no entiendes o que no aportan?
