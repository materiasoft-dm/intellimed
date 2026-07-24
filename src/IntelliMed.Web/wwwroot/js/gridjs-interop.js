/**
 * Grid.js interop for Blazor WebAssembly
 * Provides resizable columns, pagination, sorting, and search via Grid.js
 */

// Stashes the ResizableTable's DotNetObjectReference so IntelliMedNavigate can call back into it.
window.IntelliMedSetDotNetRef = function (dotNetRef) {
    window._intelliMedDotNet = dotNetRef;
};

// Global navigation callback for View buttons
window.IntelliMedNavigate = function (id) {
    // Navigate using Blazor NavigationManager via history push
    window.location.hash = '#/clients/search?focus=' + id;
    // Also try the dotnet callback
    if (window._intelliMedDotNet) {
        try {
            window._intelliMedDotNet.invokeMethodAsync('NavigateToClient', id);
        } catch (e) {
            console.log('NavigateToClient fallback:', e);
        }
    }
};

window.IntelliMedGrid = {
    instances: {},

    /**
     * Create a Grid.js table in the given element.
     * @param {string} id - Unique element ID
     * @param {object} options - Grid.js config (columns, data, pagination, search, sort, resizable)
     */
    create: function (id, options) {
        // Destroy existing instance if any
        this.destroy(id);

        // Process columns: mark HTML columns with formatter
        const columns = (options.columns || []).map(function (col, idx) {
            // Grid.js column ids must be plain alphanumeric — strip punctuation from whatever id
            // we were given (column names like "D.O.B." or "Medicare#" otherwise produce invalid
            // ids like "d.o.b." or "medicare#").
            const sanitizedId = (col.id || col.name || '').toLowerCase().replace(/[^a-z0-9]/g, '');
            const c = {
                name: col.name,
                id: sanitizedId || ('col' + idx),
                sort: col.sort !== undefined ? col.sort : true,
                width: col.width || undefined
            };
            // If the column name is empty (action column), use a special formatter
            if (col.name === '') {
                c.sort = false;
                c.formatter = function (cell) {
                    return gridjs.html(cell);
                };
            }
            // If the column data contains HTML tags, use html formatter
            return c;
        });

        // Detect which columns contain HTML by checking first data row
        const htmlColumnIndices = [];
        if (options.data && options.data.length > 0) {
            const firstRow = options.data[0];
            for (let i = 0; i < firstRow.length; i++) {
                const val = firstRow[i];
                if (typeof val === 'string' && /<[a-z][\s\S]*>/i.test(val)) {
                    htmlColumnIndices.push(i);
                }
            }
        }

        // Apply html formatter to columns that contain HTML
        columns.forEach(function (col, idx) {
            if (htmlColumnIndices.includes(idx)) {
                col.formatter = function (cell) {
                    return gridjs.html(cell);
                };
            }
        });

        // Build Grid.js config
        const config = {
            columns: columns,
            data: options.data || [],
            pagination: options.pagination || { limit: 20 },
            search: options.search !== undefined ? options.search : true,
            sort: options.sort !== undefined ? options.sort : true,
            resizable: options.resizable !== undefined ? options.resizable : true,
            fixedHeader: true,
            height: options.height || undefined,
            className: {
                table: 'gridjs-table table-sm',
                header: 'gridjs-header',
                footer: 'gridjs-footer'
            },
            style: {
                table: {
                    'font-size': '0.8125rem'
                },
                th: {
                    'font-size': '0.8125rem',
                    'padding': '0.3rem 0.5rem'
                },
                td: {
                    'font-size': '0.8125rem',
                    'padding': '0.3rem 0.5rem'
                }
            }
        };

        const grid = new gridjs.Grid(config);
        grid.render(document.getElementById(id));
        this.instances[id] = grid;
    },

    /**
     * Update data in an existing Grid.js instance.
     */
    updateData: function (id, data) {
        const grid = this.instances[id];
        if (grid) {
            grid.updateConfig({ data: data }).forceRender();
        }
    },

    /**
     * Destroy a Grid.js instance.
     */
    destroy: function (id) {
        const grid = this.instances[id];
        if (grid) {
            grid.destroy();
            delete this.instances[id];
        }
    },

    /**
     * Destroy all instances.
     */
    destroyAll: function () {
        for (const id in this.instances) {
            this.instances[id].destroy();
        }
        this.instances = {};
    }
};