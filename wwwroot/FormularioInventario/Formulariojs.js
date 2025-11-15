
        var productos = [];

        document.getElementById('precioCompra').addEventListener('input', calcularMargen);
        document.getElementById('precioVenta').addEventListener('input', calcularMargen);

        function calcularMargen() {
            var precioCompra = parseFloat(document.getElementById('precioCompra').value) || 0;
            var precioVenta = parseFloat(document.getElementById('precioVenta').value) || 0;
            
            if (precioCompra === 0) {
                document.getElementById('margen').value = '0.00';
                return;
            }

            var margen = ((precioVenta - precioCompra) / precioCompra) * 100;
            document.getElementById('margen').value = margen.toFixed(2);
        }

        function agregarProducto() {
            var codigo = document.getElementById('codigo').value.trim();
            var descripcion = document.getElementById('descripcion').value.trim();
            var precioCompra = parseFloat(document.getElementById('precioCompra').value);
            var precioVenta = parseFloat(document.getElementById('precioVenta').value);
            var cantidad = parseInt(document.getElementById('cantidad').value);

            if (!codigo || !descripcion || !precioCompra || !precioVenta || !cantidad) {
                alert('Por favor complete todos los campos');
                return;
            }

            var margen = ((precioVenta - precioCompra) / precioCompra) * 100;
            var subtotal = precioVenta * cantidad;

            var producto = {
                id: Date.now(),
                codigo: codigo,
                descripcion: descripcion,
                precioCompra: precioCompra,
                precioVenta: precioVenta,
                margen: margen.toFixed(2),
                cantidad: cantidad,
                subtotal: subtotal.toFixed(2)
            };

            productos.push(producto);
            actualizarTabla();
            limpiarFormulario();
        }

        function eliminarProducto(id) {
            productos = productos.filter(function(p) { return p.id !== id; });
            actualizarTabla();
        }

        function actualizarTabla() {
            const tbody = document.getElementById('productosTableBody');
            const productsCard = document.getElementById('productsCard');


            tbody.innerHTML = '';

            if (!productos || productos.length === 0) {
                productsCard.classList.remove('show');
                document.getElementById('totalProductos').textContent = '0';
                document.getElementById('totalValor').textContent = '$0.00';
                document.getElementById('totalFooter').textContent = '$0.00';
                return;
            }

            productsCard.classList.add('show');

            productos.forEach((p) => {
                const margenClass = p.margen >= 0 ? 'margin-positive' : 'margin-negative';
                const tr = document.createElement('tr');
                tr.innerHTML =
                    '<td class="code-cell">' + p.codigo + '</td>' +
                    '<td class="description-cell">' + p.descripcion + '</td>' +
                    '<td class="text-right">$' + parseFloat(p.precioCompra).toFixed(2) + '</td>' +
                    '<td class="text-right">$' + parseFloat(p.precioVenta).toFixed(2) + '</td>' +
                    '<td class="text-right"><span class="margin-badge ' + margenClass + '">' + p.margen + '%</span></td>' +
                    '<td class="text-right">' + p.cantidad + '</td>' +
                    '<td class="text-right subtotal-cell">$' + p.subtotal + '</td>' +
                    '<td class="text-center"><button class="delete-btn" onclick="eliminarProducto(' + p.id + ')" title="Eliminar producto">&#128465;</button></td>';
                tbody.appendChild(tr);
            });

            const total = productos.reduce((sum, p) => sum + parseFloat(p.subtotal), 0);
            document.getElementById('totalProductos').textContent = productos.length;
            document.getElementById('totalValor').textContent = '$' + total.toFixed(2);
            document.getElementById('totalFooter').textContent = '$' + total.toFixed(2);
        }

        function limpiarFormulario() {
            document.getElementById('codigo').value = '';
            document.getElementById('descripcion').value = '';
            document.getElementById('precioCompra').value = '';
            document.getElementById('precioVenta').value = '';
            document.getElementById('cantidad').value = '';
            document.getElementById('margen').value = '0.00';
        }

        function generarReporte() {
            if (productos.length === 0) {
                alert('No hay productos en el inventario para generar el reporte');
                return;
            }

            var totalInventario = productos.reduce(function(sum, p) { return sum + parseFloat(p.subtotal); }, 0);
            var totalUnidades = productos.reduce(function(sum, p) { return sum + p.cantidad; }, 0);

            alert('REPORTE DE INVENTARIO\n\nTotal de productos: ' + productos.length + '\nTotal de unidades: ' + totalUnidades + '\nValor total: $' + totalInventario.toFixed(2));
        }


        document.getElementById("btnBuscar").addEventListener("click", buscarProductos);
        document.getElementById("btnLimpiar").addEventListener("click", limpiarBusqueda);

        async function buscarProductos() {
            const q = document.getElementById("txtBuscar").value.trim();
            const msg = document.getElementById("searchMsg");

            if (!q) {
                msg.textContent = "Ingrese un texto para buscar.";
                msg.style.color = "red";
                return;
            }

            msg.textContent = "Buscando…";
            msg.style.color = "#6a737d";

            try {
                const response = await fetch(`/products/search?q=${encodeURIComponent(q)}`);

                if (!response.ok) {
                    msg.textContent = "Error al buscar productos.";
                    msg.style.color = "red";
                    return;
                }

                const data = await response.json();

                if (data.total === 0) {
                    msg.textContent = "No se encontraron productos.";
                    msg.style.color = "red";
                    actualizarTablaBusqueda([]); 
                    return;
                }

                msg.textContent = `Resultados encontrados: ${data.total}`;
                msg.style.color = "green";

                actualizarTablaBusqueda(data.items);

            } catch (error) {
                msg.textContent = "Error de conexion con el servidor.";
                msg.style.color = "red";
            }
        }

        function limpiarBusqueda() {
            document.getElementById("txtBuscar").value = "";
            document.getElementById("searchMsg").textContent = "";
            actualizarTablaBusqueda([]);
        }


        function actualizarTablaBusqueda(items) {
            const tbody = document.getElementById("productosTableBody");

            tbody.innerHTML = "";

            items.forEach(p => {
                const tr = document.createElement("tr");

                tr.innerHTML = `
                    <td>${p.sku}</td>
                    <td>${p.name}</td>
                    <td class="text-right">$${p.unitPrice.toFixed(2)}</td>
                    <td class="text-right">-</td>
                    <td class="text-right">-</td>
                    <td class="text-right">-</td>
                    <td class="text-right">$${p.unitPrice.toFixed(2)}</td>
                    <td class="text-center">N/A</td>
                `;

                tbody.appendChild(tr);
            });
        }
        document.getElementById('btnAgregar').addEventListener('click', agregarProducto);
        document.getElementById('btnReporte').addEventListener('click', generarReporte);


