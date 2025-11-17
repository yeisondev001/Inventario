// ============================================
// VARIABLES GLOBALES
// ============================================
let productos = [];
let charts = {};
// Titulos de vistas
const viewTitles = {
    'dashboard': 'Dashboard',
    'estadisticas': 'Estadisticas',
    'add-product': 'Agregar Producto',
    'products': 'Gestion de Productos'
};

// ============================================
// FUNCION PRINCIPAL DE INICIALIZACION
// ============================================
document.addEventListener('DOMContentLoaded', function () {
    inicializarApp();
});

async function inicializarApp() {
    inicializarEventos();
    await cargarProductos();
    actualizarStats();
    crearGraficasDashboard();
}

// ============================================
// INICIALIZAR EVENTOS
// ============================================
function inicializarEventos() {
    // Navegacion
    document.querySelectorAll('.nav-item[data-view]').forEach(item => {
        item.addEventListener('click', function () {
            const viewId = this.getAttribute('data-view');
            showView(viewId);
        });
    });

    // Toggle sidebar
    document.getElementById('menuToggle').addEventListener('click', toggleSidebar);

    // Boton logout
    document.getElementById('btnLogout').addEventListener('click', logout);

    // Boton nuevo producto
    document.getElementById('btnNuevoProducto').addEventListener('click', function () {
        showView('add-product');
    });

    // Formulario
    document.getElementById('formProducto').addEventListener('submit', function (e) {
        e.preventDefault();
        agregarProducto();
    });

    // Calcular margen
    document.getElementById('precioCompra').addEventListener('input', calcularMargen);
    document.getElementById('precioVenta').addEventListener('input', calcularMargen);

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
// NAVEGACION ENTRE VISTAS
// ============================================
function showView(viewId) {
    // Ocultar todas las vistas
    document.querySelectorAll('.view-section').forEach(v => v.classList.remove('active'));
    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));

    // Mostrar vista seleccionada
    const view = document.getElementById('view-' + viewId);
    if (view) {
        view.classList.add('active');
    }

    // Actualizar nav item activo
    const navItem = document.querySelector(`.nav-item[data-view="${viewId}"]`);
    if (navItem) {
        navItem.classList.add('active');
    }

    // Actualizar titulo
    document.getElementById('pageTitle').textContent = viewTitles[viewId] || 'Dashboard';

    // Si es la vista de dashboard, crear/actualizar graficas del dashboard
    if (viewId === 'dashboard') {
        setTimeout(() => {
            crearGraficasDashboard();
        }, 100);
    }

    // Si es la vista de estadisticas, crear/actualizar graficas
    if (viewId === 'estadisticas') {
        setTimeout(() => {
            crearGraficas();
        }, 100);
    }

    // Cerrar sidebar en mobile
    if (window.innerWidth <= 768) {
        document.getElementById('sidebar').classList.remove('show');
    }
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
        sessionStorage.clear();
        localStorage.clear();
        window.location.href = '/login.html';
    }
}

// ============================================
// FORMATEO DE NUMEROS
// ============================================
function formatNumber(number) {
    return new Intl.NumberFormat('en-US', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(number);
}

// ============================================
// CALCULAR MARGEN
// ============================================
function calcularMargen() {
    const precioCompra = parseFloat(document.getElementById('precioCompra').value) || 0;
    const precioVenta = parseFloat(document.getElementById('precioVenta').value) || 0;
    if (precioCompra === 0) {
        document.getElementById('margen').value = '0.00';
        return;
    }
    const margen = ((precioVenta - precioCompra) / precioCompra) * 100;
    document.getElementById('margen').value = margen.toFixed(2);
}

// ============================================
// CARGAR PRODUCTOS DESDE LA API
// ============================================
async function cargarProductos() {
    try {
        const response = await fetch('/products');
        if (!response.ok) {
            throw new Error('Error al cargar productos');
        }
        const data = await response.json();
        productos = data.map(p => ({
            id: p.id,
            codigo: p.sku,
            descripcion: p.name,
            precioCompra: p.purchasePrice || 0,
            precioVenta: p.unitPrice,
            margen: p.purchasePrice > 0 ? ((p.unitPrice - p.purchasePrice) / p.purchasePrice) * 100 : 0,
            cantidad: p.stock || 0
        }));
        actualizarTablas();
        actualizarStats();
        crearGraficasDashboard();
    } catch (error) {
        console.error('Error cargando productos:', error);
        alert('Error al cargar los productos. Verifique la conexión con el servidor.');
    }
}

// ============================================
// AGREGAR PRODUCTO
// ============================================
async function agregarProducto() {
    const codigo = document.getElementById('codigo').value.trim();
    const descripcion = document.getElementById('descripcion').value.trim();
    const precioCompra = parseFloat(document.getElementById('precioCompra').value);
    const precioVenta = parseFloat(document.getElementById('precioVenta').value);
    const cantidad = parseInt(document.getElementById('cantidad').value);

    if (!codigo || !descripcion || isNaN(precioCompra) || isNaN(precioVenta) || isNaN(cantidad)) {
        alert('Por favor complete todos los campos correctamente');
        return;
    }
    if (precioCompra <= 0 || precioVenta <= 0 || cantidad <= 0) {
        alert('Los valores deben ser mayores a 0');
        return;
    }

    try {
        const productoData = {
            sku: codigo,
            name: descripcion,
            description: descripcion,
            purchasePrice: precioCompra,
            unitPrice: precioVenta,
            categoryId: 1
        };

        const responseProducto = await fetch('/products', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(productoData)
        });

        if (!responseProducto.ok) {
            const errorData = await responseProducto.json();
            throw new Error(errorData.message || 'Error al crear producto');
        }

        const productoCreado = await responseProducto.json();

        const movementData = {
            productId: productoCreado.id,
            warehouseId: 1,
            type: 1,
            quantity: cantidad,
            movementDate: new Date().toISOString(),
            reference: 'Inventario inicial'
        };

        const responseMovement = await fetch('/movements', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(movementData)
        });

        if (!responseMovement.ok) {
            const errorData = await responseMovement.json();
            throw new Error(errorData.message || 'Error al registrar inventario inicial');
        }

        alert('Producto agregado exitosamente');
        limpiarFormulario();
        await cargarProductos();
        showView('products');
    } catch (error) {
        console.error('Error:', error);
        alert('Error al guardar el producto: ' + error.message);
    }
}

// ============================================
// ELIMINAR PRODUCTO
// ============================================
async function eliminarProducto(id) {
    const producto = productos.find(p => p.id === id);
    if (!producto) {
        alert('Producto no encontrado');
        return;
    }

    const mensaje = 'ADVERTENCIA: ¿Está seguro de eliminar este producto?\n\n' +
        'Código: ' + producto.codigo + '\n' +
        'Descripción: ' + producto.descripcion + '\n' +
        'Cantidad en stock: ' + producto.cantidad + '\n\n' +
        'Esta acción no se puede deshacer.';

    if (!confirm(mensaje)) {
        return;
    }

    try {
        const response = await fetch('/products/' + id, {
            method: 'DELETE',
            headers: { 'Content-Type': 'application/json' }
        });

        const responseText = await response.text();
        if (!response.ok) {
            let errorMessage = 'Error al eliminar el producto';
            try {
                const errorData = JSON.parse(responseText);
                errorMessage = errorData.message || errorData.Message || errorMessage;
            } catch (e) {
                errorMessage = responseText || 'Error ' + response.status + ': ' + response.statusText;
            }
            if (errorMessage.includes('movimientos de inventario')) {
                alert('NO SE PUEDE ELIMINAR ESTE PRODUCTO\n\n' +
                    'Razón: El producto tiene movimientos de inventario registrados.\n\n' +
                    'Soluciones:\n' +
                    '1. Contacte al administrador del sistema\n' +
                    '2. El producto puede ser desactivado en lugar de eliminado\n\n' +
                    'Los productos con historial de movimientos no pueden eliminarse para mantener la integridad de los datos.');
                return;
            }
            throw new Error(errorMessage);
        }

        alert('ÉXITO: Producto eliminado correctamente');
        await cargarProductos();
    } catch (error) {
        console.error('Error al eliminar producto:', error);
        alert('ERROR AL ELIMINAR\n\n' + error.message);
    }
}

// ============================================
// ACTUALIZAR TABLAS
// ============================================
function actualizarTablas() {
    actualizarTablaDashboard();
    actualizarTablaProductos();
}

function actualizarTablaDashboard() {
    const tbody = document.getElementById('dashboardTable');
    if (productos.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" style="text-align: center; color: #999;">No hay productos registrados</td></tr>';
        return;
    }
    tbody.innerHTML = productos.slice(0, 5).map(p => {
        const margen = p.margen || 0;
        const margenClass = margen >= 0 ? 'positive' : 'negative';
        return `<tr>
            <td>${p.codigo}</td>
            <td>${p.descripcion}</td>
            <td>$${formatNumber(p.precioCompra)}</td>
            <td>$${formatNumber(p.precioVenta)}</td>
            <td><span class="badge ${margenClass}">${margen.toFixed(2)}%</span></td>
            <td>${formatNumber(p.cantidad)}</td>
            <td>$${formatNumber(p.precioVenta * p.cantidad)}</td>
        </tr>`;
    }).join('');
}

function actualizarTablaProductos() {
    const tbody = document.getElementById('productosTableBody');
    if (productos.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" style="text-align: center; color: #999;">No hay productos registrados</td></tr>';
        return;
    }
    tbody.innerHTML = productos.map(p => {
        const margen = p.margen || 0;
        const margenClass = margen >= 0 ? 'positive' : 'negative';
        const subtotal = p.precioVenta * p.cantidad;
        return `<tr>
            <td>${p.codigo}</td>
            <td>${p.descripcion}</td>
            <td>$${formatNumber(p.precioCompra)}</td>
            <td>$${formatNumber(p.precioVenta)}</td>
            <td><span class="badge ${margenClass}">${margen.toFixed(2)}%</span></td>
            <td>${formatNumber(p.cantidad)}</td>
            <td>$${formatNumber(subtotal)}</td>
            <td><button class="action-btn" onclick="eliminarProducto(${p.id})">Eliminar</button></td>
        </tr>`;
    }).join('');
}

// ============================================
// ACTUALIZAR ESTADISTICAS
// ============================================
function actualizarStats() {
    const totalProductos = productos.length;
    const totalUnidades = productos.reduce((sum, p) => sum + (p.cantidad || 0), 0);
    const totalValor = productos.reduce((sum, p) => sum + (p.precioVenta * (p.cantidad || 0)), 0);
    const productosActivos = productos.filter(p => p.cantidad > 0).length;
    const margenPromedio = productos.length > 0
        ? productos.reduce((sum, p) => sum + (p.margen || 0), 0) / productos.length
        : 0;

    document.getElementById('totalProductos').textContent = formatNumber(totalProductos);
    document.getElementById('totalUnidades').textContent = formatNumber(totalUnidades);
    document.getElementById('totalValor').textContent = '$' + formatNumber(totalValor);
    document.getElementById('productosActivos').textContent = formatNumber(productosActivos);
    document.getElementById('margenPromedio').textContent = margenPromedio.toFixed(2) + '%';
    document.getElementById('stats-totalProductos').textContent = formatNumber(totalProductos);
    document.getElementById('stats-totalValor').textContent = '$' + formatNumber(totalValor);
    document.getElementById('stats-stockTotal').textContent = formatNumber(totalUnidades);
    document.getElementById('stats-margenPromedio').textContent = margenPromedio.toFixed(2) + '%';
}

// ============================================
// CREAR GRAFICAS DEL DASHBOARD
// ============================================
function crearGraficasDashboard() {
    if (productos.length === 0) return;
    crearGraficaDashboardTop5();
    crearGraficaDashboardRentabilidad();
}

function crearGraficaDashboardTop5() {
    const ctx = document.getElementById('chartDashboardTop5');
    if (!ctx) return;
    if (charts.dashboardTop5) charts.dashboardTop5.destroy();

    const topProductos = [...productos]
        .sort((a, b) => (b.precioVenta * b.cantidad) - (a.precioVenta * a.cantidad))
        .slice(0, 5);

    const ctx2d = ctx.getContext('2d');
    const gradients = Array(5).fill().map((_, i) => {
        const gradient = ctx2d.createLinearGradient(0, 0, 0, 300);
        const colors = ['#2563eb', '#10b981', '#f59e0b', '#8b5cf6', '#ec4899'];
        gradient.addColorStop(0, colors[i] + 'ff');
        gradient.addColorStop(1, colors[i] + '99');
        return gradient;
    });

    charts.dashboardTop5 = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: topProductos.map(p => p.descripcion.length > 15 ? p.descripcion.substring(0, 15) + '...' : p.descripcion),
            datasets: [{
                label: 'Valor Total en Inventario',
                data: topProductos.map(p => p.precioVenta * p.cantidad),
                backgroundColor: gradients,
                borderRadius: 12,
                borderSkipped: false,
                barThickness: 50
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(30, 58, 95, 0.95)',
                    padding: 16,
                    borderRadius: 10,
                    titleFont: { size: 15, weight: 'bold' },
                    bodyFont: { size: 14 },
                    callbacks: {
                        title: ctx => topProductos[ctx[0].dataIndex].descripcion,
                        label: ctx => {
                            const p = topProductos[ctx.dataIndex];
                            return [
                                `Valor: $${formatNumber(ctx.parsed.x)}`,
                                `Stock: ${formatNumber(p.cantidad)} unidades`,
                                `P. Venta: $${formatNumber(p.precioVenta)}`,
                                `Margen: ${p.margen.toFixed(2)}%`
                            ];
                        }
                    }
                }
            },
            scales: {
                x: {
                    beginAtZero: true,
                    grid: { color: 'rgba(209, 213, 219, 0.2)', drawBorder: false },
                    border: { display: false },
                    ticks: {
                        color: '#6b7280',
                        font: { size: 12 },
                        callback: value => '$' + formatNumber(value)
                    }
                },
                y: {
                    grid: { display: false },
                    border: { display: false },
                    ticks: { color: '#374151', font: { size: 13, weight: '600' } }
                }
            }
        }
    });
}

function crearGraficaDashboardRentabilidad() {
    const ctx = document.getElementById('chartDashboardRentabilidad');
    if (!ctx) return;
    if (charts.dashboardRentabilidad) charts.dashboardRentabilidad.destroy();

    const productosRentabilidad = productos.map(p => ({
        ...p,
        gananciaPotencial: (p.precioVenta - p.precioCompra) * p.cantidad,
        inversionTotal: p.precioCompra * p.cantidad
    }));

    const topRentabilidad = productosRentabilidad
        .filter(p => p.gananciaPotencial > 0)
        .sort((a, b) => b.gananciaPotencial - a.gananciaPotencial)
        .slice(0, 8);

    if (topRentabilidad.length === 0) return;

    const colors = ['#2563eb', '#10b981', '#f59e0b', '#8b5cf6', '#ec4899', '#ef4444', '#3b82f6', '#a855f7'];

    charts.dashboardRentabilidad = new Chart(ctx, {
        type: 'scatter',
        data: {
            datasets: [{
                label: 'Productos',
                data: topRentabilidad.map(p => ({ x: p.inversionTotal, y: p.gananciaPotencial, producto: p })),
                backgroundColor: topRentabilidad.map((_, i) => colors[i % colors.length] + 'b3'),
                borderColor: topRentabilidad.map((_, i) => colors[i % colors.length] + 'ff'),
                borderWidth: 2,
                pointRadius: 8,
                pointHoverRadius: 12
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(30, 58, 95, 0.95)',
                    padding: 16,
                    borderRadius: 10,
                    titleFont: { size: 15, weight: 'bold' },
                    bodyFont: { size: 13 },
                    callbacks: {
                        title: ctx => ctx[0].raw.producto.descripcion,
                        label: ctx => {
                            const p = ctx.raw.producto;
                            const roi = p.inversionTotal > 0 ? ((p.gananciaPotencial / p.inversionTotal) * 100) : 0;
                            return [
                                `Inversión: $${formatNumber(ctx.parsed.x)}`,
                                `Ganancia Potencial: $${formatNumber(ctx.parsed.y)}`,
                                `ROI: ${roi.toFixed(1)}%`,
                                `Stock: ${formatNumber(p.cantidad)} unidades`,
                                `Margen: ${p.margen.toFixed(2)}%`
                            ];
                        }
                    }
                }
            },
            scales: {
                x: {
                    type: 'linear',
                    position: 'bottom',
                    title: { display: true, text: 'Inversión Total ($)', color: '#374151', font: { size: 13, weight: 'bold' } },
                    grid: { color: 'rgba(209, 213, 219, 0.2)', drawBorder: false },
                    border: { display: false },
                    ticks: { color: '#6b7280', font: { size: 11 }, callback: v => '$' + formatNumber(v) }
                },
                y: {
                    title: { display: true, text: 'Ganancia Potencial ($)', color: '#374151', font: { size: 13, weight: 'bold' } },
                    grid: { color: 'rgba(209, 213, 219, 0.2)', drawBorder: false },
                    border: { display: false },
                    ticks: { color: '#6b7280', font: { size: 11 }, callback: v => '$' + formatNumber(v) }
                }
            }
        }
    });
}

// ============================================
// CREAR GRAFICAS DE ESTADISTICAS
// ============================================
function crearGraficas() {
    if (productos.length === 0) return;
    crearGraficaDistribucion();
    crearGraficaTopProductos();
    crearGraficaMargenes();
    crearGraficaRangoPrecios();
}

function crearGraficaDistribucion() {
    const ctx = document.getElementById('chartDistribucion');
    if (!ctx) return;
    if (charts.distribucion) charts.distribucion.destroy();

    const stockBajo = productos.filter(p => p.cantidad < 10).length;
    const stockMedio = productos.filter(p => p.cantidad >= 10 && p.cantidad < 50).length;
    const stockAlto = productos.filter(p => p.cantidad >= 50).length;

    charts.distribucion = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: ['Stock Bajo (<10)', 'Stock Medio (10-50)', 'Stock Alto (>50)'],
            datasets: [{
                data: [stockBajo, stockMedio, stockAlto],
                backgroundColor: ['rgba(239, 68, 68, 0.8)', 'rgba(245, 158, 11, 0.8)', 'rgba(16, 185, 129, 0.8)'],
                borderWidth: 0,
                hoverOffset: 15
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            cutout: '65%',
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: { color: '#374151', padding: 20, font: { size: 13, weight: '600' }, usePointStyle: true, pointStyle: 'circle' }
                },
                tooltip: {
                    backgroundColor: 'rgba(30, 58, 95, 0.95)',
                    padding: 12,
                    borderRadius: 8,
                    titleFont: { size: 14, weight: 'bold' },
                    bodyFont: { size: 13 },
                    callbacks: {
                        label: ctx => {
                            const total = ctx.dataset.data.reduce((a, b) => a + b, 0);
                            const percentage = ((ctx.parsed / total) * 100).toFixed(1);
                            return ` ${ctx.parsed} productos (${percentage}%)`;
                        }
                    }
                }
            }
        }
    });
}

function crearGraficaTopProductos() {
    const ctx = document.getElementById('chartTopProductos');
    if (!ctx) return;
    if (charts.topProductos) charts.topProductos.destroy();

    const topProductos = [...productos]
        .sort((a, b) => (b.precioVenta * b.cantidad) - (a.precioVenta * a.cantidad))
        .slice(0, 5);

    const colores = ['rgba(37, 99, 235, 0.8)', 'rgba(16, 185, 129, 0.8)', 'rgba(245, 158, 11, 0.8)', 'rgba(139, 92, 246, 0.8)', 'rgba(236, 72, 153, 0.8)'];

    charts.topProductos = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: topProductos.map(p => p.descripcion.length > 20 ? p.descripcion.substring(0, 20) + '...' : p.descripcion),
            datasets: [{
                label: 'Valor Total',
                data: topProductos.map(p => p.precioVenta * p.cantidad),
                backgroundColor: colores,
                borderRadius: 8,
                borderSkipped: false
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(30, 58, 95, 0.95)',
                    padding: 12,
                    borderRadius: 8,
                    titleFont: { size: 14, weight: 'bold' },
                    callbacks: { label: ctx => ` $${formatNumber(ctx.parsed.y)}` }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: { color: '#6b7280', font: { size: 12 }, callback: v => '$' + formatNumber(v) },
                    grid: { color: 'rgba(209, 213, 219, 0.3)', drawBorder: false },
                    border: { display: false }
                },
                x: {
                    ticks: { color: '#6b7280', font: { size: 11 } },
                    grid: { display: false },
                    border: { display: false }
                }
            }
        }
    });
}

function crearGraficaMargenes() {
    const ctx = document.getElementById('chartMargenes');
    if (!ctx) return;
    if (charts.margenes) charts.margenes.destroy();

    const productosOrdenados = [...productos].sort((a, b) => b.margen - a.margen);

    charts.margenes = new Chart(ctx, {
        type: 'line',
        data: {
            labels: productosOrdenados.map((p, i) => p.codigo || 'P' + (i + 1)),
            datasets: [{
                label: 'Margen (%)',
                data: productosOrdenados.map(p => p.margen),
                borderColor: 'rgba(139, 92, 246, 1)',
                backgroundColor: 'rgba(139, 92, 246, 0.1)',
                tension: 0.4,
                fill: true,
                pointRadius: 4,
                pointHoverRadius: 6,
                pointBackgroundColor: 'rgba(139, 92, 246, 1)',
                pointBorderColor: '#fff',
                pointBorderWidth: 2,
                borderWidth: 3
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            interaction: { intersect: false, mode: 'index' },
            plugins: {
                legend: {
                    labels: { color: '#374151', font: { size: 13, weight: '600' }, usePointStyle: true }
                },
                tooltip: {
                    backgroundColor: 'rgba(30, 58, 95, 0.95)',
                    padding: 12,
                    borderRadius: 8,
                    titleFont: { size: 14, weight: 'bold' },
                    callbacks: {
                        label: ctx => {
                            const p = productosOrdenados[ctx.dataIndex];
                            return [
                                ` ${p.descripcion}`,
                                ` Margen: ${ctx.parsed.y.toFixed(2)}%`,
                                ` P.Compra: $${formatNumber(p.precioCompra)}`,
                                ` P.Venta: $${formatNumber(p.precioVenta)}`
                            ];
                        }
                    }
                }
            },
            scales: {
                y: {
                    grid: { display: false },
                    border: { display: false },
                    ticks: { color: '#374151', font: { size: 13, weight: '600' } }
                },
                x: {
                    grid: { display: false },
                    border: { display: false },
                    ticks: { color: '#6b7280', maxRotation: 45, minRotation: 45, font: { size: 10 } }
                }
            }
        }
    });
}

function crearGraficaRangoPrecios() {
    const ctx = document.getElementById('chartRangoPrecios');
    if (!ctx) return;
    if (charts.rangoPrecios) charts.rangoPrecios.destroy();

    const rangos = { '0-50': 0, '51-100': 0, '101-500': 0, '501-1000': 0, '1000+': 0 };
    productos.forEach(p => {
        if (p.precioVenta <= 50) rangos['0-50']++;
        else if (p.precioVenta <= 100) rangos['51-100']++;
        else if (p.precioVenta <= 500) rangos['101-500']++;
        else if (p.precioVenta <= 1000) rangos['501-1000']++;
        else rangos['1000+']++;
    });

    const context2d = ctx.getContext('2d');
    const gradients = ['#10b981', '#2563eb', '#f59e0b', '#ef4444', '#8b5cf6'].map(color => {
        const g = context2d.createLinearGradient(0, 0, 0, 300);
        g.addColorStop(0, color + 'cc');
        g.addColorStop(1, color + '66');
        return g;
    });

    charts.rangoPrecios = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: ['0-50', '51-100', '101-500', '501-1000', '1000+'],
            datasets: [{
                label: 'Cantidad de Productos',
                data: Object.values(rangos),
                backgroundColor: gradients,
                borderRadius: 8,
                borderSkipped: false
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(30, 58, 95, 0.95)',
                    padding: 12,
                    borderRadius: 8,
                    titleFont: { size: 14, weight: 'bold' },
                    callbacks: {
                        label: ctx => {
                            const total = ctx.dataset.data.reduce((a, b) => a + b, 0);
                            const percentage = total > 0 ? ((ctx.parsed.y / total) * 100).toFixed(1) : 0;
                            return ` ${ctx.parsed.y} productos (${percentage}%)`;
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: { color: '#6b7280', stepSize: 1, font: { size: 12 } },
                    grid: { color: 'rgba(209, 213, 219, 0.3)', drawBorder: false },
                    border: { display: false }
                },
                x: {
                    ticks: { color: '#6b7280', font: { size: 12, weight: '600' } },
                    grid: { display: false },
                    border: { display: false }
                }
            }
        }
    });
}

// ============================================
// LIMPIAR FORMULARIO
// ============================================
function limpiarFormulario() {
    document.getElementById('codigo').value = '';
    document.getElementById('descripcion').value = '';
    document.getElementById('precioCompra').value = '';
    document.getElementById('precioVenta').value = '';
    document.getElementById('cantidad').value = '';
    document.getElementById('margen').value = '0.00';
}