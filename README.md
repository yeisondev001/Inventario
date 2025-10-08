# 🌐 Sistema de Inventario Web

## 🧾 Descripción general

El Sistema de Inventario Web es una aplicación desarrollada para gestionar de manera eficiente los productos, existencias y movimientos de inventario dentro de una organización. Su propósito es optimizar los procesos de control, reducir errores humanos y ofrecer una plataforma moderna y accesible desde cualquier dispositivo conectado a internet.

Este sistema combina un backend sólido desarrollado en C#, una interfaz dinámica construida con HTML, CSS y JavaScript, y una base de datos administrada con Prisma ORM, lo que permite integrar rapidez, estabilidad y escalabilidad en un mismo entorno.

Gracias a su diseño modular y su arquitectura basada en API REST, el sistema facilita la comunicación entre el cliente y el servidor, garantizando una experiencia fluida tanto para administradores como para usuarios comunes.

## 🚀 Despliegue

El sistema está pensado para ejecutarse directamente desde un servidor web, por lo que no requiere instalación adicional por parte del usuario final.  
El frontend se aloja como un sitio estático, mientras que el backend y la base de datos se ejecutan en el servidor o en la nube.

El flujo de funcionamiento general es el siguiente:

1. El usuario accede a la URL pública del sistema.
2. El navegador carga los archivos del frontend (HTML, CSS y JavaScript).
3. Las solicitudes se envían al servidor mediante la API REST desarrollada en C#.
4. Prisma gestiona todas las operaciones con la base de datos, como consultas, inserciones o actualizaciones.

## 🧰 Tecnologías utilizadas

### 💻 Backend

- Lenguaje: C# (.NET)
- Arquitectura: API REST
- ORM: Prisma
- Patrones de diseño: Repositorio y Servicio
- Autenticación (opcional): JSON Web Tokens (JWT)

### 🎨 Frontend

- HTML5: estructura semántica del contenido.
- CSS3: diseño adaptable y responsivo.
- JavaScript (ES6): manejo de eventos, interacción dinámica y conexión con la API del backend.

### 🗄️ Base de datos

- Prisma ORM, compatible con bases de datos como MySQL, PostgreSQL o SQLite, dependiendo del entorno de despliegue.
- Migraciones automáticas definidas en el archivo `schema.prisma` para mantener la consistencia de los datos.

## Funcionalidades principales

- 📦 Gestión de productos: permite registrar, modificar y eliminar artículos.
- 📊 Control de existencias: muestra el stock actual disponible.
- 🔄 Movimientos de inventario: controla entradas, salidas y ajustes.
- 🔍 Búsqueda y filtrado: por nombre, categoría o código del producto.
- 📈 Panel de control: ofrece indicadores e información resumida del inventario.
- 👥 Gestión de usuarios (opcional): asignación de roles y permisos según nivel de acceso.

## 🤝 Contribuidores

- [yeisondev001](https://github.com/yeisondev001)
- [Emil61](https://github.com/Emil61)
- [EmilEchavarria](https://github.com/EmilEchavarria)
- [enmanuelmvp](https://github.com/enmanuelmvp)
- [Fennex10](https://github.com/Fennex10)

## ⚠️ Estado del proyecto
Actualmente en desarrollo activo. Algunas funcionalidades pueden cambiar.


## 📝 Licencia 

Este proyecto está bajo la licencia MIT. Consulta el archivo [LICENSE](./LICENSE) para más detalles.

