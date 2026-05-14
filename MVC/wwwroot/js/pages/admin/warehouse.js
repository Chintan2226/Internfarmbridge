(() => {
    const API_BASE = window.API_BASE || 'http://localhost:5020/api/Admin';
    let allWarehouses = [];

    document.addEventListener('DOMContentLoaded', loadData);

    // ── LOAD ────────────────────────────────────
    async function loadData() {
        try {
            if (typeof FBSkeleton !== 'undefined') {
                FBSkeleton.show('#wh-grid', 6, 'warehouse');
            }
            const [kpiRes, gridRes] = await Promise.all([
                authFetch(`${API_BASE}/kpi`),
                authFetch(`${API_BASE}/warehouses/all`)
            ]);

            if (kpiRes.ok) {
                const kpiJson = await kpiRes.json();
                const kpi = kpiJson.data || kpiJson.Data;
                if (kpi) {
                    document.getElementById('kpi-total').textContent = kpi.totalWarehouses ?? '0';
                    document.getElementById('kpi-cap').textContent = Number(kpi.totalCapacityMt || 0).toLocaleString('en-IN') + ' MT';
                    document.getElementById('kpi-util').textContent = (kpi.averageUtilization ?? '0') + '%';
                    document.getElementById('kpi-near').textContent = kpi.nearCapacityCount ?? '0';
                }
            }

            if (gridRes.ok) {
                const response = await gridRes.json();
                allWarehouses = (response.data || []).map(w => {
                    const cap = Number(w.capacityMt ?? w.CapacityMt ?? w.totalCapacity ?? w.TotalCapacity ?? 0);
                    const used = Number(w.usedMt ?? w.UsedMt ?? w.used ?? w.Used ?? 0);
                    const avail = Number(w.availableMt ?? w.AvailableMt ?? w.available ?? w.Available ?? 0);
                    const util = Number(w.utilizationPercentage ?? w.UtilizationPercentage ?? w.utilization ?? w.Utilization ?? 0);
                    const isActiveRaw = w.isActive ?? w.IsActive ?? true;
                    const isActive = (isActiveRaw === true || isActiveRaw === "true");

                    return {
                        ...w,
                        id: w.id || w.warehouseId || w.Id,
                        name: w.name || w.Name || 'Unknown Warehouse',
                        region: w.region || w.Region || w.state || w.State || '',
                        type: w.type || w.Type || w.district || w.District || '',
                        status: (w.status || w.Status || '').toLowerCase(),
                        isActive: isActive,
                        capacityMt: cap,
                        usedMt: used,
                        availableMt: avail,
                        utilizationPercentage: util
                    };
                });
                renderGrid(allWarehouses);
            } else {
                renderEmpty('Failed to load warehouses.');
            }
        } catch (err) {
            console.error(err);
            renderEmpty('API connection error.');
            showToast('API connection error', true);
        }
    }

    // ── RENDER GRID ─────────────────────────────
    function renderGrid(data) {
        const container = document.getElementById('wh-grid');
        if (!container) return;
        if (!data || !data.length) {
            container.innerHTML = `
            <div class="empty-state">
                <svg fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 10V11"/></svg>
                <p>No warehouses found.</p>
            </div>`;
            return;
        }

        container.innerHTML = data.map(w => {
            const isInactive = !w.isActive;
            let statusClass = 'operational';
            let statusText = 'Operational';
            let barColor = 'var(--green-main)';

            if (isInactive) {
                statusClass = 'stopped';
                statusText = 'Stopped Temporarily';
                barColor = 'var(--text-muted)';
            } else if (w.utilizationPercentage > 85) {
                statusClass = 'near-capacity';
                statusText = 'Near Capacity';
                barColor = 'var(--red)';
            } else if (w.utilizationPercentage > 70) {
                statusClass = 'high-load';
                statusText = 'High Load';
                barColor = 'var(--amber)';
            }
            const util = Math.min(Number(w.utilizationPercentage) || 0, 100);

            return `
            <div class="wh-card ${isInactive ? 'inactive' : ''}">
                <div class="wh-header">
                    <div>
                        <div class="wh-name">${escHtml(w.name)}</div>
                        <div class="wh-id">ID #${w.id}${w.region ? ' · ' + escHtml(w.region) : ''}</div>
                    </div>
                    <span class="wh-status ${statusClass}">${statusText}</span>
                </div>
                <div class="wh-body">
                    <div><div class="wh-label">STATE</div>    <div class="wh-val">${escHtml(w.region) || '—'}</div></div>
                    <div><div class="wh-label">DISTRICT</div> <div class="wh-val">${escHtml(w.type) || '—'}</div></div>
                    <div><div class="wh-label">CAPACITY</div> <div class="wh-val">${Number(w.capacityMt).toLocaleString('en-IN')} MT</div></div>
                    <div><div class="wh-label">AVAILABLE</div><div class="wh-val">${Number(w.availableMt).toLocaleString('en-IN')} MT</div></div>
                </div>
                <div class="progress-sec">
                    <div class="progress-text">
                        <span>Utilization — ${Number(w.usedMt).toLocaleString('en-IN')} MT used</span>
                        <span style="color:${barColor};font-weight:800;">${w.utilizationPercentage}%</span>
                    </div>
                    <div class="progress-bar-bg">
                        <div class="progress-bar-fill" style="width:${util}%;background:${barColor};"></div>
                    </div>
                </div>
                <div class="card-actions">
                    <button class="btn-outline btn-edit" title="Edit Warehouse" onclick="openEditModal(${w.id})">
                        <svg width="15" height="15" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/></svg>
                    </button>
                    <button class="btn-outline btn-manage" 
                        ${isInactive ? 'disabled title="Warehouse is stopped"' : `onclick="openManageModal(${w.id})"`}>
                        <svg width="15" height="15" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 002-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"/></svg>
                        ${isInactive ? 'Stock Locked' : 'Manage Stock'}
                    </button>
                </div>
            </div>`;
        }).join('');
    }

    function renderEmpty(msg) {
        document.getElementById('wh-grid').innerHTML = `<div class="empty-state"><p>${escHtml(msg)}</p></div>`;
    }

    // ── MANAGE STOCK MODAL ──────────────────────
    async function openManageModal(id) {
        const w = allWarehouses.find(x => x.id === id);
        if (!w) return;

        document.getElementById('m-title').textContent = w.name;
        document.getElementById('m-subtitle').textContent = `ID #${w.id}${w.region ? ' · ' + w.region : ''}`;

        document.getElementById('m-cap').textContent = '...';
        document.getElementById('m-used').textContent = '...';
        document.getElementById('m-avail').textContent = '...';
        document.getElementById('m-util').textContent = '...';

        document.getElementById('tab-stock').innerHTML = '<div style="grid-column:1/-1;text-align:center;padding:24px;color:var(--text-muted);">Loading stock…</div>';
        switchTab('tab-stock', document.getElementById('tab-btn-stock'));

        document.getElementById('whModalOverlay').classList.add('open');

        try {
            const [invRes, infoRes] = await Promise.all([
                authFetch(`${API_BASE}/${id}/inventory`),
                authFetch(`${API_BASE}/${id}/Info`)
            ]);

            if (invRes.ok) {
                const invData = await invRes.json();
                if (invData.success) {
                    const data = invData.data || invData.Data || {};
                    const totalCap = data.totalCapacity ?? data.TotalCapacity ?? 0;
                    const usedCap = data.used ?? data.Used ?? 0;
                    const availCap = data.available ?? data.Available ?? 0;
                    const utilPct = data.utilization ?? data.Utilization ?? 0;

                    document.getElementById('m-cap').textContent = Number(totalCap).toLocaleString('en-IN') + ' MT';
                    document.getElementById('m-used').textContent = Number(usedCap).toLocaleString('en-IN') + ' MT';
                    document.getElementById('m-avail').textContent = Number(availCap).toLocaleString('en-IN') + ' MT';
                    document.getElementById('m-util').textContent = utilPct + '%';

                    const stock = data.stock ?? data.Stock ?? [];
                    document.getElementById('btnDownloadInv').onclick = () => downloadInventoryPDF(w.name, stock);

                    document.getElementById('tab-stock').innerHTML = stock.length
                        ? stock.map(s => {
                            const crop = s.cropName || s.CropName || 'Unknown';
                            const grade = s.grade || s.Grade || '—';
                            const qty = Number(s.quantity || s.Quantity || 0);
                            const unit = s.unit || s.Unit || 'MT';
                            const pct = totalCap > 0 ? Math.min((qty / totalCap) * 100, 100).toFixed(1) : 0;

                            return `
                            <div class="stock-card">
                                <div class="stock-head">
                                    <div><div class="stock-name">${escHtml(crop)}</div><div class="stock-cat">${escHtml(s.variety || '—')}</div></div>
                                    <div class="stock-grade ${grade.toUpperCase().startsWith('B') ? 'grade-b' : ''}">${escHtml(grade)}</div>
                                </div>
                                <div class="stock-qty">${qty.toLocaleString('en-IN')} <span>${escHtml(unit)}</span></div>
                                <div class="progress-sec" style="margin-top:12px;">
                                    <div class="progress-bar-bg" style="height:4px;"><div class="progress-bar-fill" style="width:${pct}%;background:var(--green-main);"></div></div>
                                    <div class="progress-text" style="font-size:10px;"><span>${pct}% of capacity</span></div>
                                </div>
                            </div>`;
                        }).join('')
                        : '<div style="grid-column:1/-1;text-align:center;color:var(--text-muted);padding:24px;">No active stock.</div>';
                }
            }

            if (infoRes.ok) {
                const infoData = await infoRes.json();
                const info = infoData.data || infoData.Data || {};
                document.getElementById('tab-info').innerHTML = `
                <div class="info-box"><div class="info-box-lbl">ADDRESS</div><div class="info-box-val">${escHtml(info.location || '—')}</div></div>
                <div class="info-box"><div class="info-box-lbl">MANAGER</div><div class="info-box-val">${escHtml(info.managerName || '—')}</div></div>`;
            }
        } catch (e) { console.error(e); }
    }

    // ── ADD/EDIT MODALS ─────────────────────────
    window.openAddModal = () => {
        document.getElementById('addWhForm').reset();
        document.getElementById('addWhModalOverlay').classList.add('open');
    };

    window.openEditModal = async (id) => {
        const w = allWarehouses.find(x => x.id === id);
        if (!w) return;

        document.getElementById('editWhId').value = w.id;
        document.getElementById('edit-wh-id-label').textContent = `Warehouse ID #${w.id}`;
        document.getElementById('eName').value = w.name || '';
        document.getElementById('eRegion').value = w.region || '';
        document.getElementById('eType').value = w.type || '';
        document.getElementById('eCap').value = w.capacityMt || '';
        document.getElementById('eStatus').value = (!w.isActive) ? 'Deactivated' : 'Operational';
        document.getElementById('eLoc').value = w.locationAddress || '';
        document.getElementById('eMgr').value = w.managerName || '';

        document.getElementById('editWhModalOverlay').classList.add('open');
        
        try {
            const res = await authFetch(`${API_BASE}/${id}/Info`);
            if (res.ok) {
                const json = await res.json();
                const d = json.data || json;
                if (d) {
                    document.getElementById('eLoc').value = d.location || d.Location || '';
                    document.getElementById('eMgr').value = d.managerName || d.ManagerName || '';
                }
            }
        } catch(e) {}
    };

    window.closeModal = (id) => document.getElementById(id).classList.remove('open');

    window.submitWarehouse = async (e, method) => {
        e.preventDefault();
        const form = e.target;
        const btn = form.querySelector('button[type="submit"]');
        const origText = btn.textContent;
        btn.disabled = true;
        btn.textContent = 'Saving...';

        const fd = new FormData(form);
        const raw = Object.fromEntries(fd.entries());
        const payload = {
            Name: raw.Name,
            Region: raw.Region,
            Type: raw.Type,
            CapacityMt: parseInt(raw.CapacityMt),
            LocationAddress: raw.LocationAddress,
            ManagerName: raw.ManagerName,
            IsActive: raw.Status !== 'Deactivated'
        };
        if (method === 'PUT') payload.Id = parseInt(raw.Id);

        const url = method === 'POST' ? `${API_BASE}/warehouse` : `${API_BASE}/warehouse/${payload.Id}`;

        try {
            const res = await authFetch(url, { method, body: JSON.stringify(payload) });
            if (res.ok) {
                showToast(`Warehouse ${method === 'POST' ? 'added' : 'updated'}!`);
                window.closeModal(method === 'POST' ? 'addWhModalOverlay' : 'editWhModalOverlay');
                loadData();
            } else {
                const err = await res.json();
                showToast(err.message || 'Error saving', true);
            }
        } catch (err) {
            showToast('Connection error', true);
        } finally {
            btn.disabled = false;
            btn.textContent = origText;
        }
    };

    window.switchTab = (tabId, el) => {
        document.querySelectorAll('.tab-item').forEach(t => t.classList.remove('active'));
        el.classList.add('active');
        document.getElementById('tab-stock').style.display = tabId === 'tab-stock' ? 'grid' : 'none';
        document.getElementById('tab-info').style.display = tabId === 'tab-info' ? 'grid' : 'none';
    };

    window.filterData = () => {
        const q = document.getElementById('search-input').value.toLowerCase();
        const status = document.getElementById('filter-status').value;
        renderGrid(allWarehouses.filter(w => {
            const matchTxt = w.name.toLowerCase().includes(q) || w.region.toLowerCase().includes(q);
            const matchStatus = !status || (status === 'active' ? w.isActive : !w.isActive);
            return matchTxt && matchStatus;
        }));
    };

    // ── PDF EXPORT ──────────────────────────────
    function downloadInventoryPDF(whName, stock) {
        const btn = document.getElementById('btnDownloadInv');
        const orig = btn.innerHTML;
        btn.innerHTML = 'Generating...';
        btn.disabled = true;

        const totalCap = document.getElementById('m-cap').textContent || '—';
        const usedCap = document.getElementById('m-used').textContent || '—';
        const availCap = document.getElementById('m-avail').textContent || '—';
        const utilPct = document.getElementById('m-util').textContent || '—';
        const now = new Date().toLocaleString('en-IN');
        const logoUrl = window.location.origin + '/Logo/Logo_Without_Bg.png';

        const rows = stock.map((s, i) => `
            <tr class="${i % 2 === 0 ? 'even' : 'odd'}">
                <td>${i + 1}</td>
                <td><strong>${s.cropName || s.CropName}</strong></td>
                <td>${s.variety || s.Variety || '—'}</td>
                <td>${s.grade || s.Grade || '—'}</td>
                <td style="text-align:right">${Number(s.quantity || s.Quantity).toLocaleString('en-IN')}</td>
                <td>${s.unit || s.Unit}</td>
            </tr>`).join('');

        const html = `
        <!DOCTYPE html>
        <html>
        <head>
            <title>Inventory Report - ${whName}</title>
            <style>
                body { font-family: sans-serif; padding: 40px; color: #333; }
                .header { display: flex; justify-content: space-between; border-bottom: 2px solid #1B7A3E; padding-bottom: 20px; }
                .logo { height: 60px; }
                .kpi-row { display: flex; gap: 20px; margin: 30px 0; }
                .kpi-card { flex: 1; background: #f8f9fa; padding: 15px; border-radius: 8px; border: 1px solid #ddd; text-align: center; }
                .kpi-val { font-size: 20px; font-weight: bold; color: #1B7A3E; margin-top: 5px; }
                table { width: 100%; border-collapse: collapse; margin-top: 20px; }
                th { background: #1B7A3E; color: white; padding: 12px; text-align: left; }
                td { padding: 10px; border-bottom: 1px solid #eee; }
                .even { background: #fff; } .odd { background: #f9f9f9; }
                .footer { margin-top: 50px; font-size: 12px; color: #777; text-align: center; }
            </style>
        </head>
        <body>
            <div class="header">
                <div><img src="${logoUrl}" class="logo"><h2>FarmBridge</h2></div>
                <div style="text-align:right"><h3>Inventory Report</h3><p>${whName}</p><p>${now}</p></div>
            </div>
            <div class="kpi-row">
                <div class="kpi-card">Capacity<div class="kpi-val">${totalCap}</div></div>
                <div class="kpi-card">Used<div class="kpi-val">${usedCap}</div></div>
                <div class="kpi-card">Available<div class="kpi-val">${availCap}</div></div>
                <div class="kpi-card">Utilization<div class="kpi-val">${utilPct}</div></div>
            </div>
            <table>
                <thead><tr><th>#</th><th>Crop</th><th>Variety</th><th>Grade</th><th>Qty</th><th>Unit</th></tr></thead>
                <tbody>${rows}</tbody>
            </table>
            <div class="footer">Confidential Report - Generated via FarmBridge Admin Portal</div>
        </body>
        </html>`;

        const win = window.open('', '_blank');
        win.document.write(html);
        win.document.close();
        setTimeout(() => { win.print(); btn.innerHTML = orig; btn.disabled = false; }, 500);
    }

    function escHtml(s) { return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;'); }
    function showToast(msg, err) {
        const t = document.getElementById('toast');
        t.textContent = msg; t.className = 'toast show ' + (err ? 'error' : '');
        setTimeout(() => t.className = 'toast', 3000);
    }

    window.openManageModal = openManageModal;
})();
