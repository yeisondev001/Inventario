# Script de Onboarding del Cliente
# Ejecutar después de desplegar la aplicación

# 1. Login como SuperAdmin (usar la contraseña generada en consola al iniciar la app por primera vez)
$loginBody = @{
    username = "admin"
    password = "TU_CONTRASENA_DE_SUPERADMIN"
} | ConvertTo-Json

$loginResponse = Invoke-RestMethod -Uri "http://localhost:5213/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $loginResponse.Token

Write-Host "Token obtenido: $token" -ForegroundColor Green

# 2. Crear Tenant (tienda del cliente)
$headers = @{
    "Authorization" = "Bearer $token"
}

$tenantBody = @{
    name = "Tienda del Cliente"
    slug = "tienda-cliente"
} | ConvertTo-Json

$tenantResponse = Invoke-RestMethod -Uri "http://localhost:5213/api/tenants" -Method Post -Body $tenantBody -ContentType "application/json" -Headers $headers
$tenantId = $tenantResponse.id

Write-Host "Tenant creado con ID: $tenantId" -ForegroundColor Green

# 3. Crear AdminTienda (usuario del cliente)
$adminBody = @{
    username = "cliente"
    email = "cliente@tienda.com"
} | ConvertTo-Json

$adminResponse = Invoke-RestMethod -Uri "http://localhost:5213/api/tenants/$tenantId/admins" -Method Post -Body $adminBody -ContentType "application/json" -Headers $headers

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DATOS DE ACCESO DEL CLIENTE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "URL: http://localhost:5213"
Write-Host "Usuario: $($adminResponse.userName)"
Write-Host "Email: $($adminResponse.email)"
Write-Host "Contraseña temporal: $($adminResponse.initialPassword)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "INSTRUCCIONES PARA EL CLIENTE:" -ForegroundColor Green
Write-Host "1. Iniciar sesión en la URL proporcionada"
Write-Host "2. Cambiar la contraseña inmediatamente"
Write-Host "3. Crear categorías, almacenes y productos"
Write-Host "4. Registrar movimientos de inventario"
Write-Host "5. Crear empleados (rol User) si es necesario"
Write-Host ""
