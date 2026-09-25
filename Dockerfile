# ==============================================================================
# Multi-Stage Dockerfile para ASP.NET Core (.NET 10) en Render.com
# ==============================================================================

# Etapa 1: Compilación y Publicación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
COPY ["PlataformaCreditos.csproj", "./"]
RUN dotnet restore "PlataformaCreditos.csproj"

# Copiar el resto del código y compilar en Release
COPY . .
RUN dotnet publish "PlataformaCreditos.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Runtime final optimizado
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Crear directorio para persistencia de base de datos SQLite (montable con Render Persistent Disk)
RUN mkdir -p /app/data
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/app.db"
ENV ASPNETCORE_ENVIRONMENT="Production"

# Copiar artefactos publicados desde la etapa de compilación
COPY --from=build /app/publish .

# Requerimiento: Configurar el comando de inicio para expandir PORT; no asumir que ${PORT} se expande dentro de una variable de entorno.
# Usamos `sh -c` con `exec` para que el shell resuelva ${PORT} dinámicamente en tiempo de ejecución.
ENTRYPOINT ["sh", "-c", "exec dotnet PlataformaCreditos.dll --urls http://0.0.0.0:${PORT:-8080}"]
