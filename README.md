# Plataforma de Gestión de Solicitudes de Crédito

Sistema web institucional desarrollado con **ASP.NET Core MVC (.NET 10)** para la gestión y evaluación de solicitudes de crédito de clientes en una entidad financiera.

## Stack Tecnológico
- **Framework:** ASP.NET Core MVC (.NET 10)
- **Autenticación y Autorización:** ASP.NET Core Identity
- **ORM & Base de Datos:** Entity Framework Core con SQLite
- **Caché y Sesiones:** Redis (Redis Cloud / Upstash)
- **Comunicación en Tiempo Real:** WebSockets (ASP.NET Hub en `/hubs/solicitudes`)
- **Mensajería Asíncrona:** RabbitMQ en CloudAMQP (AMQPS)
- **Despliegue e Infraestructura:** Render.com (Web Service en contenedor Docker)
- **Control de Versiones:** Git & GitHub Flow (ramas por requerimiento y Pull Requests hacia `main`)

---

## Estructura de Ramas y Preguntas de Evaluación
1. `feature/bootstrap-dominio`: Bootstrap con Identity, modelos de dominio, SQLite y Seed data.
2. `feature/catalogo-solicitudes`: Catálogo "Mis solicitudes", filtros y vista detalle.
3. `feature/solicitudes`: Registro de solicitudes y validaciones de negocio.
4. `feature/sesion-redis`: Sesión distribuida (última solicitud en layout) y caché Redis (60s).
5. `feature/panel-analista`: Panel `/Analista` con rol, reglas de aprobación y rechazo.
6. `feature/websocket-notificaciones`: Notificaciones en tiempo real dirigidas mediante WebSocket.
7. `feature/cloudmq-notificaciones`: Publicación con Publisher Confirms y consumo en BackgroundService con ACK manual e idempotencia.
8. `deploy/render`: Configuración para despliegue en Render.com con Dockerfile.

---

## Estado del Proyecto
- Inicialización del repositorio base.
