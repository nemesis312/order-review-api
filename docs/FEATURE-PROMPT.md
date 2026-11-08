# Prompt de la funcionalidad: cancelación de órdenes

> Este prompt se ejecuta **después**, en una sesión separada de Claude Code, sobre la rama `feature/cancel-orders-ai`. No se ejecuta en el baseline (`main`): allí no debe existir el endpoint de cancelación.

## Prompt (literal)

```text
Implementa la cancelación de órdenes mediante POST /api/orders/{id}/cancel.

Solo usuarios autorizados pueden cancelar órdenes. Una orden enviada, entregada o previamente cancelada no puede cancelarse. La razón de cancelación es obligatoria. Sigue las convenciones existentes, reutiliza funcionalidades disponibles, maneja errores apropiadamente y agrega pruebas automatizadas.

Antes de comenzar, inspecciona el repositorio. No modifiques archivos innecesarios. No hagas merge.
```
