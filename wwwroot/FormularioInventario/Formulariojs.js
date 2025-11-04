
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
            var tbody = document.getElementById('productosTableBody');
            var productsCard = document.getElementById('productsCard');
            
            if (productos.length === 0) {
                productsCard.classList.remove('show');
                return;
            }

            productsCard.classList.add('show');
            tbody.innerHTML = '';

            productos.forEach(function(producto, index) {
                var tr = document.createElement('tr');
                var margenClass = producto.margen >= 0 ? 'margin-positive' : 'margin-negative';
                
                tr.innerHTML = 
                    '<td class="code-cell">' + producto.codigo + '</td>' +
                    '<td class="description-cell">' + producto.descripcion + '</td>' +
                    '<td class="text-right">$' + parseFloat(producto.precioCompra).toFixed(2) + '</td>' +
                    '<td class="text-right">$' + parseFloat(producto.precioVenta).toFixed(2) + '</td>' +
                    '<td class="text-right">' +
                        '<span class="margin-badge ' + margenClass + '">' + producto.margen + '%</span>' +
                    '</td>' +
                    '<td class="text-right">' + producto.cantidad + '</td>' +
                    '<td class="text-right subtotal-cell">$' + producto.subtotal + '</td>' +
                    '<td class="text-center">' +
                        '<button class="delete-btn" onclick="eliminarProducto(' + producto.id + ')" title="Eliminar producto">' +
                            '&#128465;' +
                        '</button>' +
                    '</td>';
                
                tbody.appendChild(tr);
            });

            var total = productos.reduce(function(sum, p) { return sum + parseFloat(p.subtotal); }, 0);
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
