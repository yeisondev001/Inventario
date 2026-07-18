// ============================================
// DASHBOARD ADMIN TIENDA
// ============================================
let productos = [];
let categorias = [];
let almacenes = [];
let empleados = [];
let movimientos = [];

const viewTitles = {
    'dashboard': 'Dashboard',
    'add-product': 'Agregar Producto',
    'products': 'Productos',
    'categorias': 'Categorías',
    'almacenes': 'Almacenes',
    'movimientos': 'Movimientos',
    'empleados': 'Empleados'
};

document.addEventListener('DOMContentLoaded', inicializarApp);

async function inicializarApp() {
    mostrarUsuario();
    inicializarEventos();
    await cargarTodo();
}

function mostrarUsuario() {
    const user = localStorage.getItem('authUser') || 'Admin Tienda';
    const role = localStorage.getItem('authRole') || 'AdminTienda';
    document.getElementById('userName').textContent = user;
    document.getElementById('userAvatar').textContent = user.charAt(0).toUpperCase();
    document.title = `Shelf - ${role}`;
}

function inicializarEventos() {
    document.querySelectorAll('.nav-item[data-view]').forEach(item => {
        item.addEventListener('click', () => showView(item.getAttribute('data-view')));
    });

    document.getElementById('menuToggle').addEventListener('click', toggleSidebar);
    document.getElementById('btnLogout').addEventListener('click', logout);
    document.getElementById('btnNuevoProducto').addEventListener('click', () => showView('add-product'));
    document.getElementById('formProducto').addEventListener('submit', agregarProducto);
    document.getElementById('formMovimiento').addEventListener('submit', registrarMovimiento);
    document.getElementById('btnNuevaCategoria').addEventListener('click', crearCategoria);
    document.getElementById('btnNuevoAlmacen').addEventListener('click', crearAlmacen);
    document.getElementById('btnNuevoEmpleado').addEventListener('click', () => abrirModalEmpleado());
    document.getElementById('formEmpleado').addEventListener('submit', guardarEmpleado);

    document.addEventListener('click', function (e) {
        if (window.innerWidth <= 768) {
            const sidebar = document.getElementById('sidebar');
            const toggle = document.getElementById('menuToggle');
            if (!sidebar.contains(e.target) && !toggle.contains(e.target)) {
                sidebar.classList.remove('show');
            }
        }
    });
}

function toggleSidebar() {
    document.getElementById('sidebar').classList.toggle('show');
}

function showView(viewId) {
    document.querySelectorAll('.view-section').forEach(v => v.classList.remove('active'));
    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
    const view = document.getElementById('view-' + viewId);
    if (view) view.classList.add('active');
    const navItem = document.querySelector(`.nav-item[data-view="${viewId}"]`);
    if (navItem) navItem.classList.add('active');
    document.getElementById('pageTitle').textContent = viewTitles[viewId] || 'Dashboard';
}

function logout() {
    Swal.fire({
        title: '¿Cerrar sesión?',
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Sí, salir',
        cancelButtonText: 'Cancelar'
    }).then(r => {
        if (r.isConfirmed) {
            localStorage.clear();
            sessionStorage.clear();
            window.location.href = '/login.html';
        }
    });
}

async function cargarTodo() {
    await Promise.all([
        cargarProductos(),
        cargarCategorias(),
        cargarAlmacenes(),
        cargarMovimientos(),
        cargarEmpleados()
    ]);
    actualizarStats();
    renderDashboard();
}

// ============================================
// PRODUCTOS
// ============================================
async function cargarProductos() {
    try {
        const r = await fetch('/products');
        if (!r.ok) throw new Error();
        productos = await r.json();
        renderProductos();
        // Refrescar selects de producto en movimiento
        const select = document.getElementById('movProducto');
        if (select) {
            select.innerHTML = '<option value="">Seleccione...</option>' +
                productos.map(p => `<option value="${p.id}">${p.sku} - ${p.name}</option>`).join('');
        }
    } catch {
        productos = [];
    }
}

function renderProductos() {
    const tbody = document.getElementById('productosTableBody');
    if (!productos.length) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;color:#999;">No hay productos</td></tr>`;
        return;
    }
    tbody.innerHTML = productos.map(p => `
        <tr>
            <td>${p.sku || ''}</td>
            <td>${p.name || ''}</td>
            <td>$${(p.purchasePrice || 0).toFixed(2)}</td>
            <td>$${(p.unitPrice || 0).toFixed(2)}</td>
            <td>${p.stock ?? 0}</td>
            <td>
                <button class="btn btn-danger" onclick="eliminarProducto(${p.id})">Eliminar</button>
            </td>
        </tr>`).join('');
}

async function agregarProducto(e) {
    e.preventDefault();
    const sku = document.getElementById('codigo').value.trim();
    const name = document.getElementById('descripcion').value.trim();
    const purchasePrice = parseFloat(document.getElementById('precioCompra').value);
    const unitPrice = parseFloat(document.getElementById('precioVenta').value);
    const categoryId = parseInt(document.getElementById('categoriaProducto').value);
    const cantidad = parseFloat(document.getElementById('cantidad').value);

    if (!sku || !name || isNaN(purchasePrice) || isNaN(unitPrice) || isNaN(categoryId)) {
        Swal.fire('Error', 'Completa todos los campos', 'error');
        return;
    }

    try {
        // 1) Crear producto
        const r = await fetch('/products', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ sku, name, description: null, purchasePrice, unitPrice, categoryId })
        });
        const data = await r.json();
        if (!r.ok) throw new Error(data.message || 'Error');

        // 2) Si hay cantidad inicial > 0, registrar movimiento de entrada
        if (cantidad > 0) {
            const primerAlmacen = almacenes[0]?.id;
            if (primerAlmacen) {
                await fetch('/movements', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        productId: data.id,
                        warehouseId: primerAlmacen,
                        type: 1,
                        quantity: cantidad,
                        reference: 'Stock inicial'
                    })
                });
            }
        }

        Swal.fire('Éxito', 'Producto agregado', 'success');
        document.getElementById('formProducto').reset();
        await cargarProductos();
        actualizarStats();
        renderDashboard();
        showView('products');
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

async function eliminarProducto(id) {
    const c = await Swal.fire({
        title: '¿Eliminar producto?',
        text: 'Se eliminarán también sus movimientos',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Sí, eliminar',
        cancelButtonText: 'Cancelar',
        confirmButtonColor: '#dc2626'
    });
    if (!c.isConfirmed) return;
    try {
        const r = await fetch(`/products/${id}/force`, { method: 'DELETE' });
        if (!r.ok) {
            const d = await r.json();
            throw new Error(d.message || 'Error');
        }
        Swal.fire('Eliminado', '', 'success');
        await cargarProductos();
        actualizarStats();
        renderDashboard();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

// ============================================
// CATEGORÍAS
// ============================================
async function cargarCategorias() {
    try {
        const r = await fetch('/categories');
        if (!r.ok) throw new Error();
        categorias = await r.json();
        renderCategorias();
        // llenar select de formulario producto
        const sel = document.getElementById('categoriaProducto');
        if (sel) {
            sel.innerHTML = '<option value="">Seleccione...</option>' +
                categorias.map(c => `<option value="${c.id}">${c.name}</option>`).join('');
        }
    } catch {
        categorias = [];
    }
}

function renderCategorias() {
    const tbody = document.getElementById('categoriasTableBody');
    if (!categorias.length) {
        tbody.innerHTML = `<tr><td colspan="4" style="text-align:center;color:#999;">No hay categorías. Crea una para empezar.</td></tr>`;
        return;
    }
    tbody.innerHTML = categorias.map(c => {
        const numProd = productos.filter(p => p.categoryId === c.id).length;
        return `
        <tr>
            <td>${c.id}</td>
            <td>${c.name}</td>
            <td>${numProd}</td>
            <td><button class="btn btn-danger" onclick="eliminarCategoria(${c.id})">Eliminar</button></td>
        </tr>`;
    }).join('');
}

async function crearCategoria() {
    const { value: name } = await Swal.fire({
        title: 'Nueva Categoría',
        input: 'text',
        inputPlaceholder: 'Nombre de la categoría',
        showCancelButton: true,
        inputValidator: v => !v && 'El nombre es obligatorio'
    });
    if (!name) return;
    try {
        const r = await fetch('/categories', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name })
        });
        if (!r.ok) {
            const d = await r.json();
            throw new Error(d.message || 'Error');
        }
        Swal.fire('Creada', '', 'success');
        await cargarCategorias();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

async function eliminarCategoria(id) {
    const c = await Swal.fire({
        title: '¿Eliminar categoría?',
        text: 'No se podrá eliminar si hay productos usándola',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Sí, eliminar',
        cancelButtonText: 'Cancelar',
        confirmButtonColor: '#dc2626'
    });
    if (!c.isConfirmed) return;
    try {
        const r = await fetch(`/categories/${id}`, { method: 'DELETE' });
        const d = await r.json();
        if (!r.ok) throw new Error(d.message || 'Error');
        Swal.fire('Eliminada', '', 'success');
        await cargarCategorias();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

// ============================================
// ALMACENES
// ============================================
async function cargarAlmacenes() {
    try {
        const r = await fetch('/warehouses');
        if (!r.ok) throw new Error();
        almacenes = await r.json();
        renderAlmacenes();
        const sel = document.getElementById('movAlmacen');
        if (sel) {
            sel.innerHTML = '<option value="">Seleccione...</option>' +
                almacenes.map(w => `<option value="${w.id}">${w.name}</option>`).join('');
        }
    } catch {
        almacenes = [];
    }
}

function renderAlmacenes() {
    const tbody = document.getElementById('almacenesTableBody');
    if (!almacenes.length) {
        tbody.innerHTML = `<tr><td colspan="4" style="text-align:center;color:#999;">No hay almacenes</td></tr>`;
        return;
    }
    tbody.innerHTML = almacenes.map(w => `
        <tr>
            <td>${w.id}</td>
            <td>${w.name}</td>
            <td>${w.location || '-'}</td>
            <td><button class="btn btn-danger" onclick="eliminarAlmacen(${w.id})">Eliminar</button></td>
        </tr>`).join('');
}

async function crearAlmacen() {
    const { value: formValues } = await Swal.fire({
        title: 'Nuevo Almacén',
        html:
            '<input id="whName" class="swal2-input" placeholder="Nombre*">' +
            '<input id="whLoc" class="swal2-input" placeholder="Ubicación">',
        focusConfirm: false,
        showCancelButton: true,
        preConfirm: () => ({
            name: document.getElementById('whName').value.trim(),
            location: document.getElementById('whLoc').value.trim()
        })
    });
    if (!formValues || !formValues.name) return;
    try {
        const r = await fetch('/warehouses', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formValues)
        });
        if (!r.ok) throw new Error('Error');
        Swal.fire('Creado', '', 'success');
        await cargarAlmacenes();
    } catch {
        Swal.fire('Error', 'No se pudo crear', 'error');
    }
}

async function eliminarAlmacen(id) {
    const c = await Swal.fire({
        title: '¿Eliminar almacén?',
        text: 'No se podrá eliminar si tiene movimientos asociados',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Sí, eliminar',
        cancelButtonText: 'Cancelar',
        confirmButtonColor: '#dc2626'
    });
    if (!c.isConfirmed) return;
    try {
        const r = await fetch(`/warehouses/${id}`, { method: 'DELETE' });
        const d = await r.json();
        if (!r.ok) throw new Error(d.message || 'Error');
        Swal.fire('Eliminado', '', 'success');
        await cargarAlmacenes();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

// ============================================
// MOVIMIENTOS
// ============================================
async function cargarMovimientos() {
    try {
        const r = await fetch('/movements');
        if (!r.ok) throw new Error();
        movimientos = await r.json();
        renderMovimientos();
    } catch {
        movimientos = [];
    }
}

function renderMovimientos() {
    const tbody = document.getElementById('movimientosTableBody');
    if (!movimientos.length) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;color:#999;">Sin movimientos</td></tr>`;
        return;
    }
    tbody.innerHTML = movimientos.map(m => {
        const fecha = new Date(m.movementDate).toLocaleString();
        const tipo = m.type === 1 || m.type === 'In' ? 'Entrada' : 'Salida';
        const color = (m.type === 1 || m.type === 'In') ? 'green' : 'red';
        return `
        <tr>
            <td>${fecha}</td>
            <td>${m.product?.name || '-'}</td>
            <td>${m.warehouse?.name || '-'}</td>
            <td style="color:${color};font-weight:600;">${tipo}</td>
            <td>${m.quantity}</td>
            <td>${m.reference || '-'}</td>
        </tr>`;
    }).join('');
}

async function registrarMovimiento(e) {
    e.preventDefault();
    const productId = parseInt(document.getElementById('movProducto').value);
    const warehouseId = parseInt(document.getElementById('movAlmacen').value);
    const type = parseInt(document.getElementById('movTipo').value);
    const quantity = parseFloat(document.getElementById('movCantidad').value);
    const reference = document.getElementById('movReferencia').value.trim();

    if (!productId || !warehouseId || !quantity) {
        Swal.fire('Error', 'Completa los campos requeridos', 'error');
        return;
    }

    try {
        const r = await fetch('/movements', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ productId, warehouseId, type, quantity, reference })
        });
        const d = await r.json();
        if (!r.ok) throw new Error(d.message || 'Error');

        Swal.fire('Registrado', 'Movimiento registrado', 'success');
        document.getElementById('formMovimiento').reset();
        await Promise.all([cargarMovimientos(), cargarProductos()]);
        actualizarStats();
        renderDashboard();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

// ============================================
// EMPLEADOS
// ============================================
async function cargarEmpleados() {
    try {
        const r = await fetch('/api/tenant/users');
        if (!r.ok) throw new Error();
        empleados = await r.json();
        renderEmpleados();
    } catch {
        empleados = [];
    }
}

function renderEmpleados() {
    const tbody = document.getElementById('empleadosTableBody');
    if (!empleados.length) {
        tbody.innerHTML = `<tr><td colspan="4" style="text-align:center;color:#999;">Sin empleados</td></tr>`;
        return;
    }
    tbody.innerHTML = empleados.map(u => `
        <tr>
            <td>${u.userName || ''}</td>
            <td>${u.email || ''}</td>
            <td>${(u.roles || []).join(', ')}</td>
            <td>
                ${u.roles && u.roles.includes('AdminTienda')
                    ? '<span style="color:#999;">—</span>'
                    : `<button class="btn btn-danger" onclick="eliminarEmpleado('${u.id}')">Eliminar</button>`}
            </td>
        </tr>`).join('');
}

function abrirModalEmpleado() {
    document.getElementById('modalEmpleado').style.display = 'flex';
}
function cerrarModalEmpleado() {
    document.getElementById('modalEmpleado').style.display = 'none';
    document.getElementById('formEmpleado').reset();
}

async function guardarEmpleado(e) {
    e.preventDefault();
    const username = document.getElementById('empUsername').value.trim();
    const email = document.getElementById('empEmail').value.trim();
    const password = document.getElementById('empPassword').value;

    if (!username || !email || !password) {
        Swal.fire('Error', 'Completa todos los campos', 'error');
        return;
    }
    if (password.length < 10) {
        Swal.fire('Error', 'La contraseña debe tener al menos 10 caracteres', 'error');
        return;
    }

    try {
        // Rol fijo "User" - AdminTienda no puede crear otros AdminTienda
        const r = await fetch('/api/tenant/users', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, email, password, role: 'User' })
        });
        const d = await r.json();
        if (!r.ok) throw new Error(d.message || (d.errors && d.errors.join(', ')) || 'Error');

        Swal.fire('Creado', 'Empleado creado', 'success');
        cerrarModalEmpleado();
        await cargarEmpleados();
        actualizarStats();
        renderDashboard();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

async function eliminarEmpleado(id) {
    const c = await Swal.fire({
        title: '¿Eliminar empleado?',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Sí, eliminar',
        cancelButtonText: 'Cancelar',
        confirmButtonColor: '#dc2626'
    });
    if (!c.isConfirmed) return;
    try {
        const r = await fetch(`/api/tenant/users/${id}`, { method: 'DELETE' });
        const d = await r.json();
        if (!r.ok) throw new Error(d.message || 'Error');
        Swal.fire('Eliminado', '', 'success');
        await cargarEmpleados();
        actualizarStats();
        renderDashboard();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

// ============================================
// STATS Y DASHBOARD
// ============================================
function actualizarStats() {
    const totalProd = productos.length;
    const totalValor = productos.reduce((s, p) => s + (p.stock || 0) * (p.unitPrice || 0), 0);
    const totalUnidades = productos.reduce((s, p) => s + (p.stock || 0), 0);
    const totalEmp = empleados.length;

    document.getElementById('totalProductos').textContent = totalProd;
    document.getElementById('totalValor').textContent = '$' + totalValor.toFixed(2);
    document.getElementById('totalUnidades').textContent = totalUnidades;
    document.getElementById('totalEmpleados').textContent = totalEmp;
}

function renderDashboard() {
    const tbody = document.getElementById('dashboardTable');
    if (!productos.length) {
        tbody.innerHTML = `<tr><td colspan="5" style="text-align:center;color:#999;">Sin productos</td></tr>`;
        return;
    }
    tbody.innerHTML = productos.slice(0, 10).map(p => `
        <tr>
            <td>${p.sku || ''}</td>
            <td>${p.name || ''}</td>
            <td>$${(p.purchasePrice || 0).toFixed(2)}</td>
            <td>$${(p.unitPrice || 0).toFixed(2)}</td>
            <td>${p.stock ?? 0}</td>
        </tr>`).join('');
}