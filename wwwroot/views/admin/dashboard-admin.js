// ============================================
// DASHBOARD SUPERADMIN (SaaS)
// ============================================
let tiendas = [];
let adminsTienda = []; // usuarios con rol AdminTienda, vamos a buscarlos por tiendas

const viewTitles = {
    'dashboard': 'Dashboard',
    'tiendas': 'Tiendas',
    'admins': 'Admins de Tienda'
};

document.addEventListener('DOMContentLoaded', inicializarApp);

async function inicializarApp() {
    mostrarUsuario();
    inicializarEventos();
    await cargarTiendas();
    await cargarAdminsTienda();
    actualizarStats();
    renderDashboard();
}

function mostrarUsuario() {
    const user = localStorage.getItem('authUser') || 'SuperAdmin';
    document.getElementById('userName').textContent = user;
    document.getElementById('userAvatar').textContent = user.charAt(0).toUpperCase();
}

function inicializarEventos() {
    document.querySelectorAll('.nav-item[data-view]').forEach(item => {
        item.addEventListener('click', () => showView(item.getAttribute('data-view')));
    });
    document.getElementById('menuToggle').addEventListener('click', toggleSidebar);
    document.getElementById('btnLogout').addEventListener('click', logout);
    document.getElementById('btnNuevaTienda').addEventListener('click', abrirModalTienda);
    document.getElementById('btnNuevoAdminTienda').addEventListener('click', abrirModalAdminTienda);
    document.getElementById('formTienda').addEventListener('submit', guardarTienda);
    document.getElementById('formAdminTienda').addEventListener('submit', guardarAdminTienda);

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
    const sidebar = document.getElementById('sidebar');
    const mainWrapper = document.getElementById('mainWrapper');
    if (window.innerWidth <= 768) sidebar.classList.toggle('show');
    else {
        sidebar.classList.toggle('hidden');
        mainWrapper.classList.toggle('expanded');
    }
}

function showView(viewId) {
    document.querySelectorAll('.view-section').forEach(v => v.classList.remove('active'));
    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
    const view = document.getElementById('view-' + viewId);
    if (view) view.classList.add('active');
    const navItem = document.querySelector(`.nav-item[data-view="${viewId}"]`);
    if (navItem) navItem.classList.add('active');
    document.getElementById('pageTitle').textContent = viewTitles[viewId] || 'Dashboard';

    if (viewId === 'dashboard') renderDashboard();
    if (viewId === 'tiendas') renderTiendas();
    if (viewId === 'admins') renderAdmins();
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

// ============================================
// TIENDAS
// ============================================
async function cargarTiendas() {
    try {
        const r = await fetch('/api/tenants');
        if (!r.ok) throw new Error();
        tiendas = await r.json();
    } catch {
        tiendas = [];
    }
}

function renderTiendas() {
    const tbody = document.getElementById('tiendasTableBody');
    if (!tiendas.length) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;color:#999;">Sin tiendas. Crea la primera.</td></tr>`;
        return;
    }
    tbody.innerHTML = tiendas.map(t => `
        <tr>
            <td>${t.id}</td>
            <td>${t.name}</td>
            <td>${t.slug || '-'}</td>
            <td>${t.active ? '✅ Activa' : '⏸ Inactiva'}</td>
            <td>${new Date(t.createdAt).toLocaleDateString()}</td>
            <td>
                <button class="btn btn-primary" onclick="abrirModalAdminTiendaPara(${t.id})">+ Admin</button>
            </td>
        </tr>`).join('');
}

function renderDashboard() {
    const tbody = document.getElementById('dashboardTiendas');
    if (!tiendas.length) {
        tbody.innerHTML = `<tr><td colspan="5" style="text-align:center;color:#999;">Sin tiendas</td></tr>`;
        return;
    }
    tbody.innerHTML = tiendas.slice(0, 5).map(t => `
        <tr>
            <td>${t.id}</td>
            <td>${t.name}</td>
            <td>${t.slug || '-'}</td>
            <td>${t.active ? '✅' : '⏸'}</td>
            <td>${new Date(t.createdAt).toLocaleDateString()}</td>
        </tr>`).join('');
}

function abrirModalTienda() {
    document.getElementById('modalTienda').style.display = 'flex';
}
function cerrarModalTienda() {
    document.getElementById('modalTienda').style.display = 'none';
    document.getElementById('formTienda').reset();
}

async function guardarTienda(e) {
    e.preventDefault();
    const name = document.getElementById('tiendaName').value.trim();
    const slug = document.getElementById('tiendaSlug').value.trim() || null;
    if (!name) {
        Swal.fire('Error', 'El nombre es obligatorio', 'error');
        return;
    }
    try {
        const r = await fetch('/api/tenants', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, slug })
        });
        const d = await r.json();
        if (!r.ok) throw new Error(d.message || 'Error');
        Swal.fire('Creada', 'Tienda creada', 'success');
        cerrarModalTienda();
        await cargarTiendas();
        actualizarStats();
        renderDashboard();
        renderTiendas();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

// ============================================
// ADMINS TIENDA
// ============================================
async function cargarAdminsTienda() {
    adminsTienda = [];
    // No hay endpoint que liste TODOS los AdminsTienda (por diseño). Iteramos por tenant.
    for (const t of tiendas) {
        try {
            const r = await fetch(`/api/tenants/${t.id}/admins`);
            if (!r.ok) continue;
            const lista = await r.json();
            lista.forEach(u => adminsTienda.push({ ...u, tenantName: t.name }));
        } catch {
            // ignorar fallos parciales
        }
    }
    actualizarStats();
    renderAdmins();
}

function renderAdmins() {
    const tbody = document.getElementById('adminsTableBody');
    if (!adminsTienda.length) {
        tbody.innerHTML = `<tr><td colspan="4" style="text-align:center;color:#999;">
            Usa el botón "+ Nuevo Admin de Tienda" o el botón "+ Admin" en una fila de Tiendas para crear un AdminTienda.
        </td></tr>`;
        return;
    }
    tbody.innerHTML = adminsTienda.map(u => `
        <tr>
            <td>${u.userName}</td>
            <td>${u.email}</td>
            <td>${u.tenantName}</td>
            <td>—</td>
        </tr>`).join('');
}

function abrirModalAdminTienda() {
    if (!tiendas.length) {
        Swal.fire('Atención', 'Primero crea al menos una tienda', 'warning');
        return;
    }
    llenarSelectTiendas();
    document.getElementById('modalAdminTienda').style.display = 'flex';
}

function abrirModalAdminTiendaPara(tenantId) {
    if (!tiendas.length) {
        Swal.fire('Atención', 'Primero crea al menos una tienda', 'warning');
        return;
    }
    llenarSelectTiendas();
    document.getElementById('adminTiendaTenant').value = tenantId;
    document.getElementById('modalAdminTienda').style.display = 'flex';
}

function llenarSelectTiendas() {
    const sel = document.getElementById('adminTiendaTenant');
    sel.innerHTML = '<option value="">Selecciona una tienda...</option>' +
        tiendas.map(t => `<option value="${t.id}">${t.name}</option>`).join('');
}

function cerrarModalAdminTienda() {
    document.getElementById('modalAdminTienda').style.display = 'none';
    document.getElementById('formAdminTienda').reset();
}

async function guardarAdminTienda(e) {
    e.preventDefault();
    const tenantId = parseInt(document.getElementById('adminTiendaTenant').value);
    const username = document.getElementById('adminTiendaUser').value.trim();
    const email = document.getElementById('adminTiendaEmail').value.trim();

    if (!tenantId || !username || !email) {
        Swal.fire('Error', 'Completa todos los campos', 'error');
        return;
    }

    try {
        const r = await fetch(`/api/tenants/${tenantId}/admins`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, email })
        });
        const d = await r.json();
        if (!r.ok) throw new Error(d.message || (d.errors && d.errors.join(', ')) || 'Error');

        const pass = d.initialPassword;
        Swal.fire({
            title: '¡AdminTienda creado!',
            html: `<p>Comparte <b>estas credenciales</b> con el cliente (no se volverán a mostrar):</p>
                   <p style="margin-top:14px;font-family:monospace;background:#f3f4f6;padding:12px;border-radius:8px;">
                        Usuario: <b>${d.userName}</b><br>
                        Email: <b>${d.email}</b><br>
                        Contraseña: <b>${pass}</b>
                   </p>`,
            icon: 'success',
            confirmButtonText: 'Copié las credenciales'
        });
        cerrarModalAdminTienda();
    } catch (err) {
        Swal.fire('Error', err.message, 'error');
    }
}

function actualizarStats() {
    document.getElementById('totalTiendas').textContent = tiendas.filter(t => t.active).length;
    // Como no tenemos lista real todavía, calculamos sobre lo que sepamos
    document.getElementById('totalAdminsTienda').textContent = adminsTienda.length;
}