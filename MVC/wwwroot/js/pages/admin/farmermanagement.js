(() => {
    const API_BASE = window.API_BASE || 'http://localhost:5020/api/Admin';
    const ADMIN_ID = 1;

    let currentPage = 1;
    let searchTimeout = null;
    let pendingDeactivateId = null;
    let currentPaymentFarmerId = null;
    let pendingApprovalPaymentId = null;

    document.addEventListener('DOMContentLoaded', () => {
        loadStats();
        loadFarmers();
    });

    async function loadStats() {
        try {
            const res = await authFetch(`${API_BASE}/dashboard`);
            if (res.ok) {
                const d = await res.json();
                document.getElementById('stat-total').textContent   = d.registeredFarmers ?? d.RegisteredFarmers ?? '0';
                document.getElementById('stat-week').textContent    = `+${d.farmersThisWeek ?? d.FarmersThisWeek ?? 0} this week`;
                document.getElementById('stat-active').textContent  = d.activeAccounts ?? d.ActiveAccounts ?? '0';
                document.getElementById('stat-rate').textContent    = `${d.activeRate ?? d.ActiveRate ?? 0}% active rate`;
                document.getElementById('stat-deact').textContent   = d.deactivated ?? d.Deactivated ?? '0';
                document.getElementById('stat-month').textContent   = `${d.deactivatedThisMonth ?? d.DeactivatedThisMonth ?? 0} this month`;
                document.getElementById('stat-pending').textContent = d.pendingVerification ?? d.PendingVerification ?? '0';
            }
        } catch (e) { console.error("Stats Error:", e); }
    }

    async function loadFarmers() {
        const query = document.getElementById('search-input').value;
        const container = document.getElementById('f-grid');
        
        if (typeof FBSkeleton !== 'undefined') {
            FBSkeleton.show('#f-grid', 8, 'farmer'); 
        } else {
            container.innerHTML = '<div style="grid-column:1/-1; text-align:center; padding:40px; color:var(--text-muted);">Loading farmers...</div>';
        }

        try {
            const res = await authFetch(`${API_BASE}/list?searchTerm=${encodeURIComponent(query)}&pageNumber=${currentPage}`);
            if (res.ok) {
                const json = await res.json();
                container.innerHTML = ''; 
                renderGrid(json.data || json.Data || []);
                document.getElementById('page-indicator').textContent = `Page ${currentPage}`;
            }
        } catch (e) {
            container.innerHTML = '<div style="grid-column:1/-1; text-align:center; color:red;">Connection Error.</div>';
        }
    }

    function renderGrid(farmers) {
        const container = document.getElementById('f-grid');
        if (!farmers.length) {
            container.innerHTML = '<div style="grid-column:1/-1; text-align:center; padding: 40px; color:var(--text-muted);">No farmers found on this page.</div>';
            return;
        }
        
        container.innerHTML = farmers.map(f => {
            const name = f.fullName || f.FullName || 'No Name';
            const initials = name.substring(0, 2).toUpperCase();
            const userId = f.userId || f.UserId || f.id || f.Id;
            const isAct = (f.isActive ?? f.IsActive ?? false);
            
            const statusHtml = isAct
                ? `<span class="status-pill pill-active"><span class="pill-dot"></span>Active</span>`
                : `<span class="status-pill pill-inactive"><span class="pill-dot"></span>Inactive</span>`;
            
            return `
            <div class="f-card">
                <div class="f-card-header">
                    <div class="avatar-circle">${initials}</div>
                    <div style="flex:1; overflow:hidden;">
                        <div class="f-name">${name}</div>
                        <div class="f-id">#FB-${userId}</div>
                    </div>
                    <div>${statusHtml}</div>
                </div>
                <div class="f-body">
                    <div><div class="f-label">Phone</div><div class="f-val">${f.phone || f.Phone || '—'}</div></div>
                    <div><div class="f-label">Location</div><div class="f-val" title="${f.location || f.Location}">${f.location || f.Location || '—'}</div></div>
                    <div style="grid-column:1/-1;"><div class="f-label">Email</div><div class="f-val">${f.email || f.Email || '—'}</div></div>
                </div>
                <div class="f-footer">
                    <div class="f-reg">Reg: ${new Date(f.registrationDate || f.RegistrationDate).toLocaleDateString('en-IN')}</div>
                    <div class="actions-row">
                        <button class="btn btn-settle" onclick="openPaymentModal(${userId})" title="Review pending settlements">
                            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
                            Settle
                        </button>
                        <button class="btn btn-view" onclick="openDetail(${userId})">View</button>
                        ${isAct
                            ? `<button class="btn btn-deactivate" onclick="triggerDeactivate(${userId})">Deactivate</button>`
                            : `<button class="btn btn-activate" onclick="updateStatus(${userId}, true, 'Manual Activation')">Activate</button>`
                        }
                    </div>
                </div>
            </div>`;
        }).join('');
    }

    async function openPaymentModal(farmerId) {
        currentPaymentFarmerId = farmerId;
        document.getElementById('paymentModalOverlay').classList.add('open');
        const tbody = document.getElementById('pending-payments-body');
        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; padding:20px;">Loading...</td></tr>';
        try {
            const res = await authFetch(`${API_BASE}/${farmerId}/pending-payments`);
            if (res.ok) {
                const json = await res.json();
                const data = json.data || json.Data;
                if (!data || data.length === 0) {
                    tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; padding:20px; color:var(--text-muted);">No pending 70% settlements found for this farmer.</td></tr>';
                    return;
                }
                tbody.innerHTML = data.map(p => {
                    const pid = p.paymentId || p.PaymentId;
                    const cname = p.cropName || p.CropName || 'Crop';
                    const total = p.totalAmount || p.TotalAmount || 0;
                    const adv = p.advancePaid || p.AdvancePaid || 0;
                    const bal = p.balancePending || p.BalancePending || 0;

                    return `
                    <tr>
                        <td>
                            <div style="font-weight:700;">${cname}</div>
                            <div style="font-size:10px; color:var(--text-muted);">ID: #${pid}</div>
                        </td>
                        <td>
                            <div style="font-size:11px; color:var(--text-muted);">Total: ₹${total.toLocaleString('en-IN')}</div>
                            <div style="font-size:11px; color:var(--green-main); font-weight:700;">Adv: ₹${adv.toLocaleString('en-IN')}</div>
                        </td>
                        <td style="color:var(--amber); font-weight:800; font-size:15px;">₹${bal.toLocaleString('en-IN')}</td>
                        <td><span style="font-size:11px; color:var(--text-muted); font-style:italic;">Auto-generated on approval</span></td>
                        <td style="text-align:right;">
                            <button class="btn-primary" style="padding:6px 12px; font-size:11px;" onclick="approvePayment(${pid})">Approve</button>
                        </td>
                    </tr>`;
                }).join('');
            }
        } catch (e) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:red;">Failed to load payments.</td></tr>';
        }
    }

    function approvePayment(paymentId) {
        pendingApprovalPaymentId = paymentId;
        document.getElementById('confirmPaymentModal').classList.add('open');
    }

    async function executePaymentApproval() {
        if (!pendingApprovalPaymentId) return;
        const btn = document.getElementById('confirmPayBtn');
        btn.textContent = 'Processing...';
        btn.disabled = true;
        try {
            const res = await authFetch(`${API_BASE}/approve-payment`, {
                method: 'PUT',
                body: JSON.stringify({ FarmerId: currentPaymentFarmerId, PaymentId: pendingApprovalPaymentId, AdminId: ADMIN_ID })
            });
            if (res.ok) {
                showToast("Payment Approved & Released Successfully!");
                closeModal('confirmPaymentModal');
                openPaymentModal(currentPaymentFarmerId);
            } else {
                showToast("Failed to approve payment", true);
            }
        } catch (e) {
            showToast("API connection error", true);
        } finally {
            btn.textContent = 'Yes, Approve';
            btn.disabled = false;
            pendingApprovalPaymentId = null;
        }
    }

    function handleSearch() {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => { currentPage = 1; loadFarmers(); }, 400);
    }
    function changePage(step) {
        if (currentPage + step > 0) { currentPage += step; loadFarmers(); }
    }

    function triggerDeactivate(id) {
        pendingDeactivateId = id;
        document.getElementById('deactReason').value = '';
        document.getElementById('deactModalOverlay').classList.add('open');
    }
    function confirmDeactivate() {
        const reason = document.getElementById('deactReason').value.trim();
        if (!reason) return showToast('Please enter a reason', true);
        updateStatus(pendingDeactivateId, false, reason);
        closeModal('deactModalOverlay');
    }
    async function updateStatus(userId, status, reason) {
        const payload = { UserID: userId, Status: status, Reason: reason, AdminId: ADMIN_ID };
        try {
            const res = await authFetch(`${API_BASE}/update-status`, {
                method: 'PUT',
                body: JSON.stringify(payload)
            });
            if (res.ok) {
                showToast(`Farmer ${status ? 'activated' : 'deactivated'} successfully`);
                loadFarmers(); loadStats();
            } else { showToast('Update failed', true); }
        } catch (e) { showToast('API connection error', true); }
    }

    async function openDetail(id) {
        try {
            const res = await authFetch(`${API_BASE}/details/${id}`);
            if (!res.ok) throw new Error('Failed to fetch');
            const json = await res.json();
            const d = json.data || json.Data;

            document.getElementById('m-title').textContent = `Farmer Profile — ${d.profile.fullName || d.profile.FullName}`;

            document.getElementById('profile-content').innerHTML = `
                <div class="info-box">
                    <div class="f-label">Full Name</div>
                    <div class="f-val">${d.profile.fullName || d.profile.FullName || '—'}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">Farmer ID</div>
                    <div class="f-val">#FB-${d.profile.farmerID || d.profile.FarmerID}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">Mobile</div>
                    <div class="f-val">${d.profile.mobileNumber || d.profile.MobileNumber || '—'}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">Email</div>
                    <div class="f-val">${d.profile.email || d.profile.Email || '—'}</div>
                </div>
                <div class="info-box" style="grid-column:1/-1;">
                    <div class="f-label">Location</div>
                    <div class="f-val">${d.profile.location || d.profile.Location || '—'}</div>
                </div>
            `;

            const b = (d.bankDetails && d.bankDetails[0]) || {};
            document.getElementById('bank-content').innerHTML = `
                <div class="info-box">
                    <div class="f-label">Bank Name</div>
                    <div class="f-val">${b.bankName || '—'}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">Account No.</div>
                    <div class="f-val">${b.accountNumber || '—'}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">IFSC Code</div>
                    <div class="f-val">${b.ifscCode || '—'}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">KYC Status</div>
                    <div class="f-val" style="color:var(--green-main); text-transform:capitalize;">${b.status || 'Pending'}</div>
                </div>
            `;

            document.getElementById('crops-body').innerHTML = (d.cropHistory && d.cropHistory.length)
                ? d.cropHistory.map(c => `
                    <tr>
                        <td>${c.productName}</td>
                        <td>${c.quantity} kg</td>
                        <td>₹${c.price}</td>
                        <td><span style="background:var(--green-light); color:var(--green-main); padding:2px 8px; border-radius:10px; font-size:10px; font-weight:700;">${c.status}</span></td>
                        <td>${new Date(c.createdAt).toLocaleDateString('en-IN')}</td>
                    </tr>`).join('')
                : `<tr><td colspan="5" style="text-align:center; padding:20px; color:var(--text-muted);">No crops listed yet.</td></tr>`;

            document.getElementById('orders-body').innerHTML = (d.orderHistory && d.orderHistory.length)
                ? d.orderHistory.map(o => `
                    <tr>
                        <td style="font-weight:700;">#ORD-${o.orderID}</td>
                        <td>${o.cropName}</td>
                        <td>₹${o.amount}</td>
                        <td><span style="background:var(--blue-bg); color:var(--blue); padding:2px 8px; border-radius:10px; font-size:10px; font-weight:700;">${o.status}</span></td>
                        <td>${new Date(o.orderDate).toLocaleDateString('en-IN')}</td>
                    </tr>`).join('')
                : `<tr><td colspan="5" style="text-align:center; padding:20px; color:var(--text-muted);">No orders found.</td></tr>`;

            switchTab('tab-profile', document.querySelector('#detailModalOverlay .tab-item'));
            document.getElementById('detailModalOverlay').classList.add('open');

        } catch(e) {
            showToast('Failed to load farmer details', true);
        }
    }

    function closeModal(id) { document.getElementById(id).classList.remove('open'); }

    function switchTab(tabId, el) {
        const modal = el.closest('.fb-modal-overlay') || document.getElementById('detailModalOverlay');
        modal.querySelectorAll('.tab-item').forEach(t => t.classList.remove('active'));
        modal.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
        el.classList.add('active');
        document.getElementById(tabId).classList.add('active');
    }

    function showToast(msg, isErr) {
        const t = document.getElementById('toast');
        t.textContent = msg;
        t.style.background = isErr ? 'var(--red)' : 'var(--green-dark)';
        t.classList.add('show');
        setTimeout(() => t.classList.remove('show'), 3000);
    }

    window.loadFarmers           = loadFarmers;
    window.handleSearch          = handleSearch;
    window.changePage            = changePage;
    window.openPaymentModal      = openPaymentModal;
    window.approvePayment        = approvePayment;
    window.executePaymentApproval = executePaymentApproval;
    window.triggerDeactivate     = triggerDeactivate;
    window.confirmDeactivate     = confirmDeactivate;
    window.updateStatus          = updateStatus;
    window.openDetail            = openDetail;
    window.closeModal            = closeModal;
    window.switchTab             = switchTab;
})();
