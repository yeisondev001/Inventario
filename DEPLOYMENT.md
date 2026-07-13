# Guía de Despliegue - InventarioApi

## Requisitos Previos

- .NET 8 SDK
- SQL Server (LocalDB, Express, o Azure SQL)
- Cuenta SMTP para envío de emails (Gmail, SendGrid, etc.)

## Pasos de Despliegue

### 1. Configurar Base de Datos

Crear la base de datos en SQL Server:

```sql
CREATE DATABASE InventarioDB;
```

### 2. Configurar Variables de Entorno

Crear archivo `appsettings.Local.json` (NO commitear):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=InventarioDB;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "JWT": {
    "SigningKey": "tu-clave-super-segura-de-al-menos-32-caracteres-aqui"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "tu-email@gmail.com",
    "Password": "tu-app-password",
    "FromEmail": "noreply@tudominio.com",
    "FromName": "Inventario Duralon"
  },
  "AppUrl": "https://tudominio.com"
}
```

**IMPORTANTE:**
- La `SigningKey` debe tener al menos 32 caracteres
- Para Gmail, usar "App Password" (no la contraseña normal)
- `AppUrl` debe ser la URL pública de tu aplicación

### 3. Restaurar y Compilar

```bash
dotnet restore
dotnet build
```

### 4. Ejecutar Migraciones

Las migraciones se ejecutan automáticamente al iniciar la aplicación.

### 5. Iniciar la Aplicación

```bash
dotnet run
```

**IMPORTANTE:** Al iniciar por primera vez, la aplicación creará:
- Los roles: `Admin`, `AdminTienda`, `User`
- El SuperAdmin con usuario `admin`
- **La contraseña del SuperAdmin se mostrará UNA SOLA VEZ en consola**

```
========================================
SUPERADMIN CREADO (anotalo, no se muestra de nuevo):
  Usuario: admin
  Password: xY9#mK2$pL5@nQ8!
========================================
```

**GUARDA ESTA CONTRASEÑA** - No se puede recuperar después.

### 6. Crear Cliente (Onboarding)

Usar el script de onboarding o hacer las llamadas manualmente:

```bash
# Ver script completo en: scripts/onboarding-cliente.ps1
```

O usar Swagger UI en `http://localhost:5213/swagger` para:
1. Login con SuperAdmin
2. POST `/api/tenants` - Crear tienda
3. POST `/api/tenants/{id}/admins` - Crear AdminTienda

## Despliegue en Producción

### MonsterASP.NET

1. Publicar la aplicación:
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. Subir archivos vía FTP al servidor

3. Configurar variables de entorno en el panel de MonsterASP

4. Configurar dominio y certificado SSL

### Azure App Service

1. Crear App Service en Azure Portal

2. Configurar Connection Strings y App Settings:
   - `ConnectionStrings__DefaultConnection`
   - `JWT__SigningKey`
   - `Smtp__Host`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromEmail`
   - `AppUrl`

3. Conectar con GitHub para despliegue automático

4. Configurar dominio personalizado y SSL

### Docker

```bash
docker build -t inventario-api .
docker run -d -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="..." \
  -e JWT__SigningKey="..." \
  -e Smtp__Host="..." \
  inventario-api
```

## Verificación

### Health Check

```bash
curl http://localhost:5213/health
```

Debe responder: `Healthy`

### Probar Login

```bash
curl -X POST http://localhost:5213/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"tu-contrasena"}'
```

## Troubleshooting

### Error: JWT:SigningKey no configurada

Agregar la variable de entorno o configurar en `appsettings.Local.json`:
```json
{
  "JWT": {
    "SigningKey": "clave-de-al-menos-32-caracteres"
  }
}
```

### Error: No se puede conectar a la base de datos

Verificar:
- SQL Server está corriendo
- Connection string es correcto
- Base de datos existe
- Usuario tiene permisos

### Error: SMTP no funciona

Verificar:
- Credenciales SMTP son correctas
- Puerto 587 está abierto
- Para Gmail: usar App Password (no contraseña normal)
- Firewall no bloquea conexiones SMTP

### La contraseña del SuperAdmin no aparece

Si ya se inició la app antes, el SuperAdmin ya existe. Opciones:
1. Eliminar la base de datos y volver a crear
2. Resetear contraseña manualmente en la base de datos

## Seguridad en Producción

- ✅ Usar HTTPS siempre
- ✅ Cambiar contraseña del SuperAdmin inmediatamente
- ✅ Usar contraseñas fuertes para SMTP y JWT
- ✅ No commitear `appsettings.Local.json`
- ✅ Configurar firewall para solo permitir puertos necesarios
- ✅ Hacer backups regulares de la base de datos
- ✅ Monitorear logs de la aplicación
