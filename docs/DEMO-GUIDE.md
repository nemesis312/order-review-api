# Guía de demo: "Reviso en vivo código que escribió la IA"

## Regla principal

**No inventes resultados ni defectos de Claude.** Todo lo que muestres como hallazgo (bueno o malo) debe salir del diff real, de la salida real de `dotnet test` o de una petición real a la API. Si Claude lo hace bien, dilo. Si no encuentras problemas, dilo. El checklist es para mirar, no para buscar culpables a la fuerza.

## 1. Preparación previa

- Instala el .NET 10 SDK y `dotnet-ef` (ver README).
- Base limpia: borra `src/OrderReview.Api/order-review.db` si existe.
- Comprueba que `main` compila y pasa: `dotnet build` y `dotnet test`.
- Ten los tokens listos (desde `src/OrderReview.Api`, con `dotnet user-jwts create`; comandos en el README). Prepara uno de solo lectura y uno con lectura y creación. Si los generas antes de grabar, no pegues los tokens en pantalla.
- Terminal: fuente grande, tema de alto contraste, ventana ancha. Dos pestañas: una para la API y otra para comandos.
- Ten abiertos `README.md`, `CLAUDE.md` y `docs/REVIEW-CHECKLIST.md`.
- Confirma que no hay endpoint de cancelación en `main`: `grep -ri cancel src/OrderReview.Api/Controllers` no debe encontrar nada.

## 2. Recorrido del baseline (en `main`)

1. Estructura: `Controllers/`, `Services/`, `Repositories/`, `Data/`, `Errors/`, `Security/`.
2. Cadena de capas: `OrdersController` -> `OrderService` -> `OrderRepository` -> `AppDbContext`.
3. Patrones que luego debería reutilizar la IA: `OrderValidationException` + `ApiExceptionHandler`, `OrderNotFoundException`, constantes de `AuthorizationPolicies`, `TimeProvider`.
4. Modelo: `CancellationReason`, `CancelledAt` y `OrderStatus.Cancelled` existen, pero nada los usa.
5. Levanta la API: `dotnet run --project src/OrderReview.Api --launch-profile http`, y muestra `http://localhost:5010/swagger`.
6. Haz un GET con token y un POST con token de solo lectura (403) usando los ejemplos del README.
7. Ejecuta `dotnet test` y muestra que pasa en verde.
8. Enseña `CLAUDE.md`: son las reglas que la IA debe seguir.

## 3. Lanzar Claude Code en la rama

En una sesión nueva, fuera del baseline:

```bash
git checkout -b feature/cancel-orders-ai
claude
```

Pega el contenido de [docs/FEATURE-PROMPT.md](FEATURE-PROMPT.md) tal cual. No lo edites en cámara. No autorices commits ni merge: la IA deja los cambios sin confirmar.

## 4. Mostrar el diff

```bash
git diff main...feature/cancel-orders-ai --stat
git diff main...
```

Si aún no hay commits en la rama y los cambios están sin confirmar, usa `git status` y `git diff` para verlos. Recorre el diff archivo por archivo, no solo el resumen.

## 5. Ejecutar pruebas

```bash
dotnet build
dotnet test
```

Muestra la salida real y el conteo real (el baseline tiene 47 pruebas; el total ahora será otro). Opcional y honesto: cambia a propósito una regla en el código y comprueba si alguna prueba falla. Si ninguna falla, ese es un hallazgo real.

## 6. Repasar el checklist

Recorre las 8 preguntas de [docs/REVIEW-CHECKLIST.md](REVIEW-CHECKLIST.md) con el diff abierto. Para cada una, señala la línea o archivo concreto que respalda tu respuesta. Cierra con tu decisión: aceptar, pedir cambios o descartar, y por qué.

## Recordatorios

- No hagas merge en cámara a menos que lo decidas tú tras la revisión.
- Para repetir la demo: `git checkout main`, borra la rama de la feature y borra `order-review.db`.
