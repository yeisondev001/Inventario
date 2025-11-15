// ============================================
// VARIABLES GLOBALES
// ============================================
let productos = [];
let productosFiltrados = [];

// ============================================
// INICIALIZACIÓN
// ============================================
document.addEventListener('DOMContentLoaded', function () {
    inicializarApp();
});

async function inicializarApp() {
    inicializarEventos();
    await cargarProductos();
    actualizarStats();
    renderizarTabla();
}

// ============================================
// EVENTOS
// ============================================
function inicializarEventos() {
    // Toggle sidebar
    document.getElementById('menuToggle').addEventListener('click', toggleSidebar);

    // Logout
    document.getElementById('btnLogout').addEventListener('click', logout);

    // Búsqueda
    document.getElementById('searchInput').addEventListener('input', filtrarProductos);

    // Cerrar sidebar en mobile al hacer click fuera
    document.addEventListener('click', function (e) {
        if (window.innerWidth <= 768) {
            const sidebar = document.getElementById('sidebar');
            const menuToggle = document.getElementById('menuToggle');
            if (!sidebar.contains(e.target) && !menuToggle.contains(e.target)) {
                sidebar.classList.remove('show');
            }
        }
    });
}

// ============================================
// TOGGLE SIDEBAR
// ============================================
function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    const mainWrapper = document.getElementById('mainWrapper');

    if (window.innerWidth <= 768) {
        sidebar.classList.toggle('show');
    } else {
        sidebar.classList.toggle('hidden');
        mainWrapper.classList.toggle('expanded');
    }
}

// ============================================
// LOGOUT
// ============================================
function logout() {
    if (confirm('¿Está seguro que desea cerrar sesión?')) {
        window.location.href = '/login.html';
    }
}

// ============================================
// FORMATEO DE NÚMEROS
// ============================================
function formatNumber(number) {
    return new Intl.NumberFormat('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(number);
}

// ============================================
// CARGAR PRODUCTOS DESDE API (OPTIMIZADO)
// ============================================
async function cargarProductos() {
    try {
        const response = await fetch('/products');
        if (!response.ok) {
            throw new Error('Error al cargar productos');
        }

        const data = await response.json();

        // ✅ OPTIMIZACIÓN: Usar directamente el stock que viene del backend
        productos = data.map(p => ({
            id: p.id,
            codigo: p.sku,
            descripcion: p.name,
            precio: p.unitPrice,
            cantidad: p.stock || 0  // El backend ya calcula el stock
        }));

        productosFiltrados = [...productos];
        actualizarStats();
        renderizarTabla();
    } catch (error) {
        console.error('Error cargando productos:', error);
        mostrarError('Error al cargar los productos. Verifique la conexión con el servidor.');
    }
}

// ============================================
// ACTUALIZAR ESTADÍSTICAS
// ============================================
function actualizarStats() {
    const totalProductos = productos.length;
    const totalUnidades = productos.reduce((sum, p) => sum + p.cantidad, 0);
    const productosDisponibles = productos.filter(p => p.cantidad > 0).length;

    document.getElementById('totalProductos').textContent = formatNumber(totalProductos);
    document.getElementById('totalUnidades').textContent = formatNumber(totalUnidades);
    document.getElementById('productosDisponibles').textContent = formatNumber(productosDisponibles);
}

// ============================================
// FILTRAR PRODUCTOS
// ============================================
function filtrarProductos() {
    const searchTerm = document.getElementById('searchInput').value.toLowerCase().trim();

    if (searchTerm === '') {
        productosFiltrados = [...productos];
    } else {
        productosFiltrados = productos.filter(p =>
            p.codigo.toLowerCase().includes(searchTerm) ||
            p.descripcion.toLowerCase().includes(searchTerm)
        );
    }

    renderizarTabla();
}

// ============================================
// RENDERIZAR TABLA
// ============================================
function renderizarTabla() {
    const tbody = document.getElementById('productosTableBody');
    const productCount = document.getElementById('productCount');

    // Actualizar contador
    productCount.textContent = `${productosFiltrados.length} ${productosFiltrados.length === 1 ? 'producto' : 'productos'}`;

    // Si no hay productos
    if (productosFiltrados.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="5" style="text-align: center; color: #999; padding: 40px;">
                    <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-bottom: 12px; opacity: 0.3;">
                        <circle cx="11" cy="11" r="8"></circle>
                        <path d="m21 21-4.35-4.35"></path>
                    </svg>
                    <div>No se encontraron productos</div>
                </td>
            </tr>
        `;
        return;
    }

    // Renderizar productos
    tbody.innerHTML = productosFiltrados.map(p => {
        const estado = obtenerEstado(p.cantidad);
        return `
            <tr>
                <td><strong>${p.codigo}</strong></td>
                <td>${p.descripcion}</td>
                <td><strong>${formatNumber(p.cantidad)}</strong></td>
                <td>$${formatNumber(p.precio)}</td>
                <td><span class="badge ${estado.clase}">${estado.texto}</span></td>
            </tr>
        `;
    }).join('');
}

// ============================================
// OBTENER ESTADO DEL PRODUCTO
// ============================================
function obtenerEstado(cantidad) {
    if (cantidad === 0) {
        return { clase: 'agotado', texto: 'Agotado' };
    } else if (cantidad < 10) {
        return { clase: 'bajo', texto: 'Stock Bajo' };
    } else {
        return { clase: 'disponible', texto: 'Disponible' };
    }
}

// ============================================
// MOSTRAR ERROR
// ============================================
function mostrarError(mensaje) {
    const tbody = document.getElementById('productosTableBody');
    tbody.innerHTML = `
        <tr>
            <td colspan="5" style="text-align: center; color: var(--danger); padding: 40px;">
                <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="margin-bottom: 12px;">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="12" y1="8" x2="12" y2="12"></line>
                    <line x1="12" y1="16" x2="12.01" y2="16"></line>
                </svg>
                <div>${mensaje}</div>
            </td>
        </tr>
    `;
}