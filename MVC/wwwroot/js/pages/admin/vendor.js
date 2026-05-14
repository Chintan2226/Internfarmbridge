(() => {
    const API_BASE = window.API_BASE || 'http://localhost:5020/api/Admin';
    let allVendors = [];

    document.addEventListener('DOMContentLoaded', loadInitialData);

    async function loadInitialData() {
        try {
            // Stats
            const kpiRes = await authFetch(`${API_BASE}/analytics/vendors/kpi`);
            if (kpiRes.ok) {
                const json = await kpiRes.json();
                const d = json.data || json;
                if (d) {
                    document.getElementById('stat-total').textContent = d.totalVendors ?? d.TotalVendors ?? '0';
                    document.getElementById('stat-value').textContent = '₹' + ((d.totalOrdersValue ?? d.TotalOrdersValue ?? 0) / 100000).toFixed(2) + ' L';
                    document.getElementById('stat-orders').textContent = d.ordersPlaced ?? d.OrdersPlaced ?? '0';
                    document.getElementById('stat-rate').textContent = (d.fulfillmentRate ?? d.FulfillmentRate ?? 0) + '%';
                }
            }

            // Vendors list
            if (typeof FBSkeleton !== 'undefined') {
                FBSkeleton.show('#vendor-grid', 8, 'farmer');
            }

            const res = await authFetch(`${API_BASE}/Vendors`);
            if (!res.ok) throw new Error("Could not fetch vendors");
            
            const json = await res.json();
            allVendors = json.data || json;

            if (Array.isArray(allVendors)) {
                renderVendorGrid();
            } else {
                throw new Error("API returned non-array data");
            }
        } catch (e) {
            console.error("Initial load error:", e);
            document.getElementById('vendor-grid').innerHTML = 
                `<div style="grid-column:1/-1; text-align:center; padding:40px; color:var(--red);">
                    <strong>Failed to load data.</strong>
                </div>`;
        }
    }

    function renderVendorGrid() {
        const grid = document.getElementById('vendor-grid');
        if (!allVendors.length) {
            grid.innerHTML = '<div style="grid-column:1/-1; text-align:center; padding:40px; color:var(--text-muted);">No vendors found.</div>';
            return;
        }
        
        grid.innerHTML = allVendors.map(v => {
            const status = (v.status || v.Status || '').toLowerCase();
            const active = (status === 'active' || status === 'approved');
            const bizName = v.businessName || v.BusinessName || 'No Business Name';
            const initials = bizName.charAt(0).toUpperCase();
            const id = v.id || v.Id;
            
            return `
            <div class="v-card" onclick="openVendorModal('${id}')">
                <div class="v-card-header">
                    <div class="avatar-circle">${initials}</div>
                    <div style="flex:1; overflow:hidden;">
                        <div class="vendor-biz" title="${bizName}">${esc(bizName)}</div>
                        <div class="vendor-id">ID: #VND-${id}</div>
                    </div>
                    <span class="status-pill ${pillCls(status)}">${statusLbl(status)}</span>
                </div>
                
                <div class="v-card-body">
                    <div>
                        <div class="v-info-label">Contact</div>
                        <div class="v-info-val">${esc(v.contactName || v.ContactName || '—')}</div>
                    </div>
                    <div>
                        <div class="v-info-label">Order Value</div>
                        <div class="v-info-val" style="color:var(--green-main)">₹${fmt(v.totalOrderValue || v.TotalOrderValue)}</div>
                    </div>
                    <div>
                        <div class="v-info-label">GSTIN</div>
                        <div class="v-info-val">${esc(v.gstNumber || v.GstNumber || 'N/A')}</div>
                    </div>
                    <div>
                        <div class="v-info-label">Reg. Date</div>
                        <div class="v-info-val">${fmtDate(v.registrationDate || v.RegistrationDate)}</div>
                    </div>
                </div>

                <div class="v-card-footer" onclick="event.stopPropagation()">
                    ${active 
                        ? `<button class="btn-sm btn-approved" disabled>✓ Active</button>` 
                        : `<button class="btn-sm btn-primary" onclick="approveVendor('${id}')">Approve Vendor</button>`
                    }
                </div>
            </div>`;
        }).join('');
    }

    window.openVendorModal = (id) => {
        const v = allVendors.find(x => (x.id || x.Id) == id);
        if (!v) return;

        const bizName = v.businessName || v.BusinessName || '';
        const vid = v.id || v.Id;
        const status = (v.status || v.Status || '').toLowerCase();

        const overlay = document.getElementById('vendorModalOverlay');
        const body = document.getElementById('modalBody');
        
        body.innerHTML = `
            <div style="text-align:center; margin-bottom:20px;">
                <div class="avatar-circle" style="width:64px; height:64px; margin:0 auto 10px; font-size:24px;">${bizName[0]}</div>
                <h2 style="font-weight:800;">${esc(bizName)}</h2>
                <p style="color:var(--text-muted); font-size:13px;">Vendor ID: #VND-${vid}</p>
            </div>

            <div class="detail-row"><span>Status</span><span class="status-pill ${pillCls(status)}">${statusLbl(status)}</span></div>
            <div class="detail-row"><span>Contact Person</span><span class="v-info-val">${esc(v.contactName || v.ContactName)}</span></div>
            <div class="detail-row"><span>GST Number</span><span class="v-info-val" style="font-family:monospace;">${esc(v.gstNumber || v.GstNumber)}</span></div>
            <div class="detail-row"><span>Registration</span><span class="v-info-val">${fmtDate(v.registrationDate || v.RegistrationDate)}</span></div>
            <div class="detail-row"><span>Total Business</span><span class="v-info-val" style="color:var(--green-main)">₹${fmt(v.totalOrderValue || v.TotalOrderValue)}</span></div>

            <div style="margin-top:24px; display:flex; gap:10px;">
                <button class="btn-sm btn-primary" style="flex:2;" onclick="closeModal()">Done</button>
            </div>
        `;

        overlay.style.display = 'flex';
        setTimeout(() => overlay.classList.add('open'), 10);
        document.body.style.overflow = 'hidden';
    };

    window.closeModal = () => {
        const overlay = document.getElementById('vendorModalOverlay');
        overlay.classList.remove('open');
        setTimeout(() => { overlay.style.display = 'none'; }, 200);
        document.body.style.overflow = '';
    };

    window.approveVendor = async (id) => {
        try {
            const res = await authFetch(`${API_BASE}/${id}/Status?status=active`, {
                method: 'PUT',
                body: JSON.stringify({ adminId: '1', reason: 'Approved' })
            });
            if (res.ok) {
                showToast('Vendor approved!');
                loadInitialData();
            }
        } catch (e) { console.error(e); }
    };

    function showToast(msg) {
        const t = document.getElementById('toast');
        const m = document.getElementById('toastMsg');
        if (m) m.textContent = msg;
        t.classList.add('show');
        setTimeout(() => t.classList.remove('show'), 3000);
    }

    // Helpers
    function esc(s) { return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;'); }
    function fmt(n) { return Number(n || 0).toLocaleString('en-IN'); }
    function fmtDate(d) { 
        if (!d || d.startsWith('0001')) return '—';
        const date = new Date(d);
        if (isNaN(date.getTime())) return '—';
        return date.toLocaleDateString('en-IN', {day:'2-digit', month:'short', year:'numeric'}); 
    }
    function pillCls(s) { return (s==='active'||s==='approved') ? 'pill-active' : (s==='inactive'?'pill-inactive':'pill-pending'); }
    function statusLbl(s) { return s === 'active' || s === 'approved' ? 'Active' : 'Pending'; }

})();
