# Plataforma de Gestión de Solicitudes de Crédito

Sistema web corporativo desarrollado con **ASP.NET Core MVC (.NET 10)** para la originación, evaluación y aprobación de créditos financieros en tiempo real, con persistencia transaccional en **SQLite / Entity Framework Core**, caché distribuida y sesiones en **Redis**, mensajería asíncrona garantizada con **CloudAMQP (RabbitMQ)**, eventos bidireccionales en vivo mediante **WebSockets (SignalR)** y contenedorizado para **Render.com**.

---

## 🏛️ Arquitectura del Sistema

```mermaid
flowchart TD
    subgraph Clientes ["🌐 Capa de Presentación (Navegadores)"]
        C1["👤 Cliente (Mis Solicitudes / Crear)"]
        C2["👔 Analista de Riesgo (/Analista)"]
    end

    subgraph AppHost ["⚙️ Contenedor ASP.NET Core (.NET 10 MVC)"]
        Proxy["ForwardedHeaders (Reverse Proxy Support)"]
        MVC["Controladores & Vistas MVC\n(Solicitudes, Analista, Home)"]
        WS["SignalR Hub (/hubs/solicitudes)\nTargeted Push to UsuarioId"]
        CacheSvc["SolicitudesCacheService\n(Caché 60s & Invalidación)"]
        Prod["RabbitMqProducer\n(AMQPS, Durable, Publisher Confirms)"]
        Cons["RabbitMqConsumerService\n(BackgroundService, Manual ACK, Deduplicación)"]
    end

    subgraph Storage ["💾 Persistencia & Servicios Cloud Externos"]
        DB[("🗄️ SQLite Database\nEF Core 10 + Identity")]
        Redis[("⚡ Redis Cloud / Upstash\nSesión Distribuida + Key-Value Cache")]
        CloudMQ[("🐰 CloudAMQP (RabbitMQ)\nQueue: solicitudes.notificaciones")]
    end

    C1 <-->|"HTTPS / WSS"| Proxy
    C2 <-->|"HTTPS / WSS"| Proxy
    Proxy --> MVC
    Proxy --> WS

    MVC -->|"Lectura / Escritura"| DB
    MVC --> CacheSvc
    CacheSvc -->|"TTL 60s / Get-Set"| Redis
    MVC -->|"Publica Evento"| Prod
    Prod -->|"BasicPublishAsync (Persistent)"| CloudMQ

    Cons -->|"BasicConsumeAsync (Manual ACK)"| CloudMQ
    Cons -->|"Registra Notificación Idempotente"| DB

    MVC -.->|"Notifica Estado en Vivo"| WS
    WS -.->|"Push Evento"| C1
```

---

## 🚀 Stack Tecnológico

| Componente | Tecnología | Propósito |
| :--- | :--- | :--- |
| **Framework Base** | ASP.NET Core MVC (.NET 10) | Framework web moderno de alto rendimiento |
| **Seguridad e Identidad** | ASP.NET Core Identity | Autenticación, cookies seguras y control de acceso basado en roles (`Analista`, `Cliente`) |
| **ORM & Persistencia** | EF Core 10 + SQLite | Mapeo objeto-relacional, migraciones automáticas, check constraints y restricciones condicionales |
| **Sesión & Caché** | Redis (`StackExchange.Redis`) | Sesión distribuida (última solicitud) y caché de consultas con TTL de 60 segundos |
| **Tiempo Real** | WebSockets / ASP.NET Core SignalR | Notificaciones push directas dirigidas al usuario propietario sin sondeo |
| **Mensajería Cloud** | CloudAMQP (RabbitMQ Client 7.x) | Publicación confiable con Publisher Confirms y consumo desacoplado en segundo plano con ACK manual |
| **Contenedorización** | Docker (Multi-stage build) | Empaquetado ligero y reproducible sobre Alpine/Debian .NET Runtime |
| **Despliegue Cloud** | Render.com (Web Service) | Hosting en la nube con soporte de puerto dinámico y terminación SSL |

---

## 🌿 Flujo de Ramas y Git Flow en GitHub

El proyecto cumple con la **estrategia de ramas obligatoria** dictada para la evaluación:
- **Protección de `main`:** Ningún commit se realizó directamente sobre `main`.
- **Ramas por Requerimiento:** Cada pregunta se desarrolló en su rama temática aislada:
  1. `feature/bootstrap-dominio`: Bootstrap con Identity, modelos `Cliente`, `SolicitudCredito`, validaciones DB, índices condicionales y seed de datos. (PR #1)
  2. `feature/catalogo-solicitudes`: Catálogo "Mis solicitudes", filtros de monto/fecha validados en servidor y vista detalle. (PR #2)
  3. `feature/solicitudes`: Formulario de registro, validaciones de ingresos y límite de solicitudes pendientes concurrentes. (PR #3)
  4. `feature/sesion-redis`: Sesión respaldada en Redis (última solicitud en layout) y caché distribuida de 60s con invalidación activa. (PR #4)
  5. `feature/panel-analista`: Dashboard `/Analista` con diagnóstico regla 5X, flujos de aprobación y rechazo modal con motivo obligatorio. (PR #5)
  6. `feature/websocket-notificaciones`: WebSockets con SignalR en `/hubs/solicitudes`, actualización en vivo en vista del cliente y reconexión automática con sincronización. (PR #6)
  7. `feature/cloudmq-notificaciones`: Integración AMQPS con CloudAMQP, Publisher Confirms, `BackgroundService` con ACK manual, deduplicación e historial `/Solicitudes/MisNotificaciones`. (PR #7)
  8. `deploy/render`: Dockerfile multi-stage, `render.yaml`, proxy headers y documentación completa. (PR #8)
- **Merge Commits:** Cada Pull Request fue integrado usando **"Create a merge commit"**, garantizando que el gráfico en **Insights -> Network Graph** de GitHub refleje la apertura y cierre explícito de cada rama.

---

## 👥 Tabla de Usuarios y Credenciales de Prueba

Al iniciar la aplicación por primera vez, el inicializador automático (`DbInitializer.SeedAsync`) aprovisiona los roles y usuarios requeridos:

| Correo Electrónico | Contraseña | Rol Asignado | Datos de Cliente Asociados | Estado Inicial |
| :--- | :--- | :--- | :--- | :--- |
| `analista@creditos.com` | `Password123!` | **Analista** | *Personal del Banco (evaluador)* | Acceso a `/Analista` |
| `cliente1@creditos.com` | `Password123!` | *(Cliente)* | Ingresos: **\$3,500.00** / Activo: Sí | 1 Solicitud Pendiente (\$5,000) |
| `cliente2@creditos.com` | `Password123!` | *(Cliente)* | Ingresos: **\$1,800.00** / Activo: Sí | 1 Solicitud Aprobada (\$3,000) |

---

## 💻 Instrucciones de Ejecución Local

### Prerrequisitos
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) instalado.
- Cuenta o instancia activa de **Redis** (Redis Cloud o Upstash) y **CloudAMQP** (plan Little Lemur o local).
- Git instalado.

### 1. Clonar el repositorio
```bash
git clone git@github.com:aspm1901/PRUEBA-PLATAFORMA_CR-DITOS.git
cd PRUEBA-PLATAFORMA_CR-DITOS
```

### 2. Configurar Secretos de Usuario (User Secrets)
Para evitar exponer credenciales en archivos de configuración rastreados por Git, use `dotnet user-secrets`:

```bash
dotnet user-secrets init
dotnet user-secrets set "Redis:ConnectionString" "TU_REDIS_CONNECTION_STRING"
dotnet user-secrets set "RabbitMq:ConnectionString" "amqps://usuario:password@host/vhost"
dotnet user-secrets set "RabbitMq:QueueName" "solicitudes.notificaciones"
dotnet user-secrets set "RabbitMq:ConsumerEnabled" "true"
```

### 3. Compilar y Ejecutar la Aplicación
```bash
dotnet restore
dotnet build
dotnet run
```
La aplicación aplicará automáticamente las migraciones de SQLite (`app.db`) e insertará los usuarios y solicitudes iniciales. Ingrese desde su navegador a `https://localhost:5001` o `http://localhost:5000`.

---

## 🧪 Guías Paso a Paso para Verificación

### A. Prueba de Notificaciones en Tiempo Real (WebSockets / SignalR)
Esta prueba valida que el cambio de estado de una solicitud realizado por un analista se refleje de inmediato en la pantalla del cliente sin necesidad de recargar el navegador.

1. **Abrir dos navegadores o una ventana normal y una ventana de incógnito:**
   - **Ventana 1 (Cliente):** Inicie sesión con `cliente1@creditos.com` / `Password123!`.
     - Vaya a **"Mis Solicitudes"** (`/Solicitudes/MisSolicitudes`).
     - Observe el indicador de conexión en la esquina superior derecha: **"🟢 Conectado en tiempo real"**.
     - La solicitud #1 figura con estado **Pendiente** (badge amarillo).
   - **Ventana 2 (Analista):** Inicie sesión con `analista@creditos.com` / `Password123!`.
     - Vaya al panel **"Panel Analista"** (`/Analista`).
     - Localice la solicitud de `cliente1@creditos.com`.
2. **Ejecutar la acción de evaluación:**
   - En la Ventana 2 (Analista), haga clic en **"Aprobar"** (o en **"Rechazar"** ingresando un motivo obligatorio).
3. **Comprobar la reactividad:**
   - En la Ventana 1 (Cliente), **sin presionar F5 ni recargar la página**:
     - Aparecerá un aviso toast flotante: *"¡Actualización de Solicitud! La solicitud #1 ha sido Aprobada/Rechazada"*.
     - El badge de estado de la fila correspondiente cambia de color y texto en vivo.
     - La fila se resalta brevemente con una animación visual de pulso.
4. **Verificar aislamiento:**
   - Si abre una tercera ventana con `cliente2@creditos.com`, notará que este usuario **no** recibe la notificación dirigida a `cliente1`, demostrando el filtrado estricto por `UsuarioId`.

---

### B. Prueba de Mensajería Asíncrona CloudAMQP (RabbitMQ)
Esta prueba valida el desacoplamiento entre el productor y el consumidor, la confirmación de entrega (*Publisher Confirms*), la persistencia de mensajes y el procesamiento idempotente con confirmación manual (*Manual ACK*).

1. **Desactivar temporalmente el consumidor en segundo plano:**
   - En su terminal local, ejecute:
     ```bash
     dotnet user-secrets set "RabbitMq:ConsumerEnabled" "false"
     ```
   - Reinicie la aplicación (`dotnet run`).
2. **Generar un evento desde la aplicación:**
   - Inicie sesión como `analista@creditos.com` y apruebe o rechace una solicitud, o cree una nueva solicitud con un cliente.
   - La aplicación emitirá el mensaje persistente a CloudAMQP y registrará en la consola:
     `[RabbitMqProducer] Mensaje publicado exitosamente con Publisher Confirm a 'solicitudes.notificaciones'.`
3. **Verificar el mensaje en el Dashboard de CloudAMQP:**
   - Ingrese a la consola web de su instancia en [CloudAMQP](https://customer.cloudamqp.com/).
   - Diríjase a la pestaña **Queues** y seleccione `solicitudes.notificaciones`.
   - Podrá constatar en los gráficos que la métrica **Ready** tiene **1 mensaje en espera** y **Unacked** está en 0.
4. **Reactivar el consumidor:**
   - En la terminal, active nuevamente el consumidor:
     ```bash
     dotnet user-secrets set "RabbitMq:ConsumerEnabled" "true"
     ```
   - Reinicie la aplicación (`dotnet run`).
   - El servicio `RabbitMqConsumerService` iniciará, consumirá el mensaje pendiente de la cola, verificará que no sea duplicado e insertará la fila en la tabla `Notificaciones` de SQLite emitiendo un `BasicAckAsync`.
5. **Verificar la cola vacía y el historial del cliente:**
   - En el panel de CloudAMQP, la cola volverá a **0 mensajes Ready**.
   - En la aplicación, ingrese con el cliente correspondiente y visite **"Mis Notificaciones"** (`/Solicitudes/MisNotificaciones`) para visualizar el registro histórico procesado.

---

## 🚢 Despliegue en Render.com

La aplicación incluye soporte nativo para despliegue automatizado en **Render.com** mediante Dockerfile y `render.yaml`.

### 1. Variables de Entorno en Render
En el panel del Web Service de Render, configure las siguientes variables de entorno:

| Variable | Valor de Ejemplo | Descripción |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Define el entorno de producción |
| `ConnectionStrings__DefaultConnection` | `Data Source=/app/data/app.db` | Ruta del archivo de base de datos SQLite |
| `Redis__ConnectionString` | `tu-redis.redis.cache.windows.net:6380,password=...,ssl=True` | Cadena de conexión a Redis Cloud |
| `RabbitMq__ConnectionString` | `amqps://user:pwd@host/vhost` | URI AMQPS provista por CloudAMQP |
| `RabbitMq__QueueName` | `solicitudes.notificaciones` | Nombre de la cola durable de mensajería |
| `RabbitMq__ConsumerEnabled` | `true` | Habilita el BackgroundService de consumo |

### 2. Manejo de Puerto Dinámico (`$PORT`)
Render asigna un puerto aleatorio a través de la variable de entorno `$PORT`. Debido a que ASP.NET Core no expande variables de entorno estilo shell dentro de sus propios parámetros de configuración por defecto, el contenedor utiliza el siguiente comando de arranque en el `Dockerfile`:

```dockerfile
ENTRYPOINT ["sh", "-c", "exec dotnet PlataformaCreditos.dll --urls http://0.0.0.0:${PORT:-8080}"]
```
Esto garantiza que la variable `$PORT` sea resuelta por la shell del sistema operativo al momento de levantar el contenedor, vinculando el servidor Kestrel exactamente al puerto esperado por el proxy de Render.

### 3. Persistencia de SQLite en Render
- **Plan Gratuito (Free Web Service):** El contenedor opera sobre un sistema de archivos efímero. Cada vez que el servicio entra en reposo por inactividad (*spin-down*) o se despliega una nueva versión, el contenedor se recrea. Gracias a `DbInitializer.SeedAsync`, la base de datos se auto-regenera con los datos de prueba base sin generar excepciones.
- **Planes con Disco Persistente (Render Disks):** Para mantener las solicitudes creadas por los usuarios entre reinicios y deploys, se debe añadir un disco persistente en Render configurado con el punto de montaje:
  - **Mount Path:** `/app/data`
  - **Size:** 1 GB o superior
  
  De este modo, el archivo `/app/data/app.db` persistirá de forma indefinida.

---

## 📂 Estructura Principal del Proyecto

```text
├── Controllers/
│   ├── AnalistaController.cs          # Panel de revisión, validación regla 5X, aprobación/rechazo
│   ├── HomeController.cs              # Página principal e informativa
│   └── SolicitudesController.cs       # Catálogo, registro, detalle y notificaciones
├── Data/
│   ├── ApplicationDbContext.cs        # DbContext, constraints CHECK, índice condicional único
│   ├── DbInitializer.cs               # Seed automático de roles, analista y clientes de prueba
│   └── Migrations/                    # Migraciones versionadas de EF Core
├── Hubs/
│   └── SolicitudesHub.cs              # SignalR Hub protegido para push targeted a clientes
├── Models/
│   ├── Cliente.cs                     # Entidad de cliente y límite de ingresos
│   ├── Notificacion.cs                # Entidad de notificación asíncrona con MessageId único
│   └── SolicitudCredito.cs            # Entidad de solicitud de crédito con máquina de estados
├── Services/
│   ├── ISolicitudesCacheService.cs    # Contrato de caché Redis
│   ├── SolicitudesCacheService.cs     # Implementación con TTL de 60s e invalidación granular
│   ├── IRabbitMqProducer.cs           # Contrato de publicación CloudAMQP
│   ├── RabbitMqProducer.cs            # Publicación AMQPS con Publisher Confirms
│   └── RabbitMqConsumerService.cs     # BackgroundService con ACK manual y deduplicación
├── Views/
│   ├── Analista/Index.cshtml          # Vista del panel de analista con modal de rechazo
│   ├── Solicitudes/Crear.cshtml       # Formulario con validación reactiva de negocio
│   ├── Solicitudes/Detalle.cshtml     # Vista detallada de solicitud y registro en sesión Redis
│   ├── Solicitudes/MisNotificaciones.cshtml # Historial de notificaciones asíncronas
│   └── Solicitudes/MisSolicitudes.cshtml # Catálogo con SignalR y reconexión automática
├── Dockerfile                         # Build multi-stage para .NET 10 y Render
├── render.yaml                        # Manifiesto de infraestructura como código para Render
└── Program.cs                         # Configuración de pipeline, middleware y DI
```

---

## 🔒 Consideraciones de Seguridad y Buenas Prácticas
1. **Zero Credentials in Repo:** No se incluyen claves, contraseñas ni cadenas de conexión en el control de versiones. Todas se inyectan mediante secretos de usuario o variables de entorno del proveedor cloud.
2. **Validación Defensiva en Servidor:** Las restricciones críticas de negocio (como el límite de 10X de ingresos, existencia de solicitud pendiente previa y rangos de búsqueda) se validan estrictamente en el backend antes de persistir o filtrar datos.
3. **Idempotencia Transaccional:** El consumidor de RabbitMQ valida la existencia previa del `MessageId` (UUID) contra SQLite antes de procesar cualquier notificación, evitando duplicidad en caso de reentrega de red.
4. **Proxy Headers Seguros:** Configuración de `ForwardedHeadersOptions` para que cookies seguras de Identity y conexiones WebSocket funcionen con total fidelidad detrás del terminador SSL de Render.
