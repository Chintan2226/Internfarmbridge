/**
 * Vendor Payments Logic
 * Handles transaction history, stats, and receipt generation.
 */

(function () {
    const API_BASE = window.API_BASE_URL || 'http://localhost:5020/api/Vendor';
    let allPayments = [];

    // --- HELPER FUNCTIONS ---
    function getCookie(name) {
        const v = `; ${document.cookie}`;
        const parts = v.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    async function authorizedFetch(url, options = {}) {
        const token = getCookie("authToken");
        options.headers = { ...options.headers, 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' };
        const response = await fetch(url, options);
        if (response.status === 401) { window.location.href = "/Vendor/Login"; return null; }
        return response;
    }

    // --- DATA LOADING ---
    async function loadPayments() {
        showSkeletons();
        
        try {
            const response = await authorizedFetch(`${API_BASE}/payments`);
            if (!response) return; 
            
            const result = await response.json();
            allPayments = result.data || result || [];
            
            displayStats(allPayments);
            filterAndDisplayTable();
            
        } catch (error) {
            console.error("Error loading payments:", error);
            renderErrorState();
        }
    }

    function showSkeletons() {
        const statsHtml = `
            <div class="payment-stat-card skeleton-active"><div class="skeleton-icon"></div><div style="flex:1"><div class="skeleton-line" style="width:40%;height:10px;margin-bottom:8px"></div><div class="skeleton-line" style="width:70%;height:20px"></div></div></div>
            <div class="payment-stat-card skeleton-active"><div class="skeleton-icon"></div><div style="flex:1"><div class="skeleton-line" style="width:40%;height:10px;margin-bottom:8px"></div><div class="skeleton-line" style="width:70%;height:20px"></div></div></div>
            <div class="payment-stat-card skeleton-active"><div class="skeleton-icon"></div><div style="flex:1"><div class="skeleton-line" style="width:40%;height:10px;margin-bottom:8px"></div><div class="skeleton-line" style="width:70%;height:20px"></div></div></div>
        `;
        const statsCont = document.getElementById('statsContainer');
        if (statsCont) statsCont.innerHTML = statsHtml;
        const tableCont = document.getElementById('tableContainer');
        if (tableCont) tableCont.innerHTML = '<div class="skeleton-table skeleton-active"></div>';
    }

    function displayStats(payments) {
        const statsCont = document.getElementById('statsContainer');
        if (!statsCont) return;

        const successPayments = payments.filter(p => p.status?.toLowerCase() === 'success' || p.status?.toLowerCase() === 'completed');
        const totalPaid = successPayments.reduce((sum, p) => sum + (p.amount || 0), 0);
        const pendingCount = payments.filter(p => p.status?.toLowerCase() === 'pending' || p.status?.toLowerCase() === 'initiated').length;

        statsCont.innerHTML = `
            <div class="payment-stat-card reveal">
                <div class="stat-icon-wrap emerald">
                    <i class="fi fi-rr-check-circle"></i>
                </div>
                <div class="stat-info">
                    <span class="stat-label" data-i18n="pay_total_settled">Total Settled</span>
                    <h2 class="stat-value">₹${totalPaid.toLocaleString('en-IN')}</h2>
                </div>
            </div>
            <div class="payment-stat-card reveal" style="transition-delay: 0.1s">
                <div class="stat-icon-wrap orange">
                    <i class="fi fi-rr-clock"></i>
                </div>
                <div class="stat-info">
                    <span class="stat-label" data-i18n="pay_pending_settlements">Pending Settlements</span>
                    <h2 class="stat-value">${pendingCount}</h2>
                </div>
            </div>
            <div class="payment-stat-card reveal" style="transition-delay: 0.2s">
                <div class="stat-icon-wrap blue">
                    <i class="fi fi-rr-receipt"></i>
                </div>
                <div class="stat-info">
                    <span class="stat-label" data-i18n="pay_total_transactions">Total Transactions</span>
                    <h2 class="stat-value">${payments.length}</h2>
                </div>
            </div>
        `;
        setTimeout(() => $(".reveal").addClass("visible"), 50);
    }

    function filterAndDisplayTable() {
        const statusFilter = document.getElementById('payStatusFilter')?.value || 'all';
        const methodFilter = document.getElementById('payMethodFilter')?.value || 'all';

        let filtered = allPayments;
        if (statusFilter !== 'all') {
            filtered = filtered.filter(p => p.status?.toLowerCase() === statusFilter);
        }
        if (methodFilter !== 'all') {
            filtered = filtered.filter(p => (p.paymentMethod || p.method || '').toLowerCase().includes(methodFilter));
        }

        renderTable(filtered);
    }

    function renderTable(payments) {
        const container = document.getElementById('tableContainer');
        if (!container) return;
        if (!payments || payments.length === 0) {
            container.innerHTML = `
                <div class="text-center py-5">
                    <i class="fi fi-rr-inbox mb-3 d-block" style="font-size: 48px; color: #cbd5e1;"></i>
                    <p class="text-muted" data-i18n="pay_no_match">No transactions matching your filters</p>
                </div>
            `;
            return;
        }

        let html = `
            <table class="premium-table">
                <thead>
                    <tr>
                        <th data-i18n="pay_table_id">TRANSACTION ID</th>
                        <th data-i18n="pay_table_order">ORDER ID</th>
                        <th data-i18n="pay_table_amount">AMOUNT</th>
                        <th data-i18n="pay_table_method">METHOD</th>
                        <th data-i18n="pay_table_status">STATUS</th>
                        <th data-i18n="pay_table_date">DATE</th>
                        <th class="text-right" data-i18n="pay_table_action">ACTION</th>
                    </tr>
                </thead>
                <tbody>
        `;

        payments.forEach(p => {
            const status = (p.status || '').toLowerCase();
            const statusClass = (status === 'success' || status === 'completed') ? 'status-success' :
                               (status === 'failed') ? 'status-failed' :
                               (status === 'initiated') ? 'status-initiated' : 'status-pending';
            
            const method = p.paymentMethod || p.method || 'N/A';
            const date = new Date(p.paymentDate || p.createdAt || new Date()).toLocaleDateString('en-IN', {
                day: '2-digit', month: 'short', year: 'numeric'
            });

            const fbT = window.fbT || (k => k);

            html += `
                <tr class="reveal">
                    <td><span class="txn-id-pill">${p.transactionId || 'N/A'}</span></td>
                    <td><a href="/Vendor/Orders" class="order-id-link">${p.orderId || 'N/A'}</a></td>
                    <td><span class="amount-text">₹${(p.amount || 0).toLocaleString('en-IN')}</span></td>
                    <td><span class="pay-method-badge">${method}</span></td>
                    <td><span class="pay-status-badge ${statusClass}">${fbT(status)}</span></td>
                    <td><span class="date-text">${date}</span></td>
                    <td class="text-right">
                        <button class="btn-receipt" onclick="window.viewReceipt('${p.transactionId}', '${p.orderId}', ${p.amount}, '${status}', '${method}')">
                            <i class="fi fi-rr-download"></i> <span data-i18n="pay_receipt">Receipt</span>
                        </button>
                    </td>
                </tr>
            `;
        });

        html += '</tbody></table>';
        container.innerHTML = html;
        if (window.fbInit) window.fbInit();
        setTimeout(() => $(".reveal").addClass("visible"), 50);
    }

    function renderErrorState() {
        const tableCont = document.getElementById('tableContainer');
        if (!tableCont) return;
        tableCont.innerHTML = `
            <div class="text-center py-5">
                <i class="fi fi-rr-triangle-warning mb-3 d-block" style="font-size: 48px; color: #ef4444;"></i>
                <p class="text-danger" data-i18n="pay_load_fail">Failed to load payment history. Please try again.</p>
                <button class="btn-receipt mt-3 mx-auto" onclick="window.loadPayments()" data-i18n="pay_retry">Retry Connection</button>
            </div>
        `;
    }

    window.viewReceipt = function (txnId, orderId, amount, status, method) {
        if (typeof Swal === 'undefined') return;
        const subtotal = amount / 1.05;
        const tax = amount - subtotal;
        const isPaid = status.toLowerCase() === 'success' || status.toLowerCase() === 'completed';

        const receiptHtml = `
            <div id="printableReceipt" style="text-align: left; padding: 20px; font-family: 'Plus Jakarta Sans', sans-serif; color: #1e293b; background: #fff; position: relative; overflow: hidden;">
                <!-- Watermark -->
                ${isPaid ? `
                <div style="position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%) rotate(-30deg); font-size: 120px; font-weight: 900; color: rgba(21, 128, 61, 0.04); pointer-events: none; text-transform: uppercase; letter-spacing: 10px; z-index: 0;">
                    PAID
                </div>
                ` : ''}

                <!-- Receipt Header -->
                <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 40px; position: relative; z-index: 1;">
                    <div style="display: flex; align-items: center; gap: 12px;">
                        <img src="/Logo/Logo_Without_Bg.png" alt="Logo" style="width: 52px; height: 52px;" />
                        <div>
                            <div style="font-size: 22px; font-weight: 800; color: #15803d; line-height: 1;">FARMBRIDGE</div>
                            <div style="font-size: 10px; color: #64748b; letter-spacing: 0.5px; margin-top: 4px;">AGRO SOLUTIONS PVT LTD</div>
                        </div>
                    </div>
                    <div style="text-align: right;">
                        <div style="font-size: 24px; font-weight: 800; color: #0f172a; letter-spacing: -0.5px;">RECEIPT</div>
                        <div style="display: flex; align-items: center; justify-content: flex-end; gap: 8px; margin-top: 4px;">
                            <span style="font-size: 12px; color: #64748b;">No:</span>
                            <span style="font-size: 12px; font-weight: 700; color: #1e293b;">#${txnId.slice(-8).toUpperCase()}</span>
                        </div>
                    </div>
                </div>

                <!-- Details Section -->
                <div style="display: grid; grid-template-columns: 1.2fr 0.8fr; gap: 40px; margin-bottom: 40px; position: relative; z-index: 1;">
                    <div style="background: #f8fafc; padding: 20px; border-radius: 16px; border: 1px solid #f1f5f9;">
                        <div style="font-size: 10px; font-weight: 800; color: #94a3b8; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px;">Billing To</div>
                        <div style="font-size: 15px; font-weight: 700; color: #1e293b; margin-bottom: 4px;">Registered Business Partner</div>
                        <div style="font-size: 12px; color: #64748b; line-height: 1.6;">
                            FarmBridge Marketplace Verified Vendor<br>
                            GSTIN: 24AAAFB1234F1Z5 (Simulated)<br>
                            Registered Office, India
                        </div>
                    </div>
                    <div style="padding: 10px 0;">
                        <div style="font-size: 10px; font-weight: 800; color: #94a3b8; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px;">Summary</div>
                        <div style="display: flex; flex-direction: column; gap: 8px;">
                            <div style="display: flex; justify-content: space-between;">
                                <span style="font-size: 12px; color: #64748b;">Order Ref:</span>
                                <span style="font-size: 12px; font-weight: 700; color: #1e293b;">${orderId}</span>
                            </div>
                            <div style="display: flex; justify-content: space-between;">
                                <span style="font-size: 12px; color: #64748b;">Method:</span>
                                <span style="font-size: 12px; font-weight: 700; color: #1e293b; text-transform: uppercase;">${method}</span>
                            </div>
                            <div style="display: flex; justify-content: space-between;">
                                <span style="font-size: 12px; color: #64748b;">Status:</span>
                                <span style="font-size: 12px; font-weight: 800; color: ${isPaid ? '#15803d' : '#f59e0b'};">${status.toUpperCase()}</span>
                            </div>
                            <div style="display: flex; justify-content: space-between;">
                                <span style="font-size: 12px; color: #64748b;">Date:</span>
                                <span style="font-size: 12px; font-weight: 700; color: #1e293b;">${new Date().toLocaleDateString('en-IN')}</span>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Itemized Table -->
                <div style="position: relative; z-index: 1; margin-bottom: 30px;">
                    <table style="width: 100%; border-collapse: collapse;">
                        <thead>
                            <tr>
                                <th style="padding: 12px 16px; text-align: left; font-size: 10px; font-weight: 800; color: #94a3b8; text-transform: uppercase; letter-spacing: 1px; border-bottom: 2px solid #f1f5f9;">Transaction Description</th>
                                <th style="padding: 12px 16px; text-align: right; font-size: 10px; font-weight: 800; color: #94a3b8; text-transform: uppercase; letter-spacing: 1px; border-bottom: 2px solid #f1f5f9;">Amount</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td style="padding: 20px 16px; border-bottom: 1px solid #f1f5f9;">
                                    <div style="font-size: 14px; font-weight: 700; color: #1e293b;">Crop Procurement Settlement</div>
                                    <div style="font-size: 12px; color: #64748b; margin-top: 4px;">Automatic settlement for Order #${orderId}</div>
                                </td>
                                <td style="padding: 20px 16px; text-align: right; font-size: 14px; font-weight: 700; color: #1e293b; border-bottom: 1px solid #f1f5f9;">
                                    ₹${subtotal.toLocaleString('en-IN', {minimumFractionDigits: 2, maximumFractionDigits: 2})}
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>

                <!-- Totals Block -->
                <div style="display: flex; justify-content: flex-end; margin-bottom: 50px; position: relative; z-index: 1;">
                    <div style="width: 260px; background: #f8fafc; padding: 20px; border-radius: 16px; border: 1px solid #f1f5f9;">
                        <div style="display: flex; justify-content: space-between; margin-bottom: 12px;">
                            <span style="font-size: 13px; color: #64748b;">Subtotal</span>
                            <span style="font-size: 13px; font-weight: 600; color: #1e293b;">₹${subtotal.toLocaleString('en-IN', {minimumFractionDigits: 2})}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between; margin-bottom: 12px; padding-bottom: 12px; border-bottom: 1px dashed #e2e8f0;">
                            <span style="font-size: 13px; color: #64748b;">Tax / TDS (5%)</span>
                            <span style="font-size: 13px; font-weight: 600; color: #1e293b;">₹${tax.toLocaleString('en-IN', {minimumFractionDigits: 2})}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between; align-items: center;">
                            <span style="font-size: 14px; font-weight: 800; color: #0f172a;">Total Amount</span>
                            <span style="font-size: 22px; font-weight: 900; color: #15803d;">₹${amount.toLocaleString('en-IN')}</span>
                        </div>
                    </div>
                </div>

                <!-- Footer -->
                <div style="display: flex; justify-content: space-between; align-items: flex-end; position: relative; z-index: 1;">
                    <div>
                        <div style="font-size: 11px; color: #64748b; line-height: 1.6;">
                            <strong style="color: #1e293b; font-size: 12px;">FarmBridge Support</strong><br>
                            farmbridge13@gmail.com | +91 1800-123-4567<br>
                            Ground Floor, Agri-Tech Park, Gujarat, India
                        </div>
                    </div>
                    <div style="text-align: right;">
                        <div style="background: #fff; padding: 8px; border: 1px solid #e2e8f0; border-radius: 12px; display: inline-block; margin-bottom: 8px;">
                            <i class="fi fi-rr-qrcode" style="font-size: 42px; color: #0f172a;"></i>
                        </div>
                        <div style="font-size: 9px; font-weight: 800; color: #15803d; text-transform: uppercase; letter-spacing: 1px;">✓ Digitally Certified Document</div>
                    </div>
                </div>
            </div>
        `;

        Swal.fire({
            title: '',
            html: receiptHtml,
            showConfirmButton: true,
            confirmButtonText: '<i class="fi fi-rr-print"></i> Download / Print Receipt',
            confirmButtonColor: '#15803d',
            showCloseButton: true,
            width: '720px',
            padding: '0',
            customClass: {
                popup: 'premium-receipt-modal'
            }
        }).then((result) => {
            if (result.isConfirmed) {
                printReceipt(receiptHtml);
            }
        });
    };

    function printReceipt(html) {
        const printWindow = window.open('', '_blank', 'width=950,height=900');
        if (!printWindow) return;
        printWindow.document.write(`
            <html>
                <head>
                    <title>Official Receipt - FarmBridge</title>
                    <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800;900&display=swap" rel="stylesheet">
                    <style>
                        body { font-family: 'Plus Jakarta Sans', sans-serif; padding: 40px; color: #1e293b; background: #fff; -webkit-print-color-adjust: exact; }
                        .receipt-outer { max-width: 800px; margin: auto; padding: 20px; border: 1px solid #f1f5f9; }
                        @media print {
                            body { padding: 0; }
                            .receipt-outer { border: none; }
                        }
                    </style>
                </head>
                <body>
                    <div class="receipt-outer">
                        ${html}
                    </div>
                    <script>
                        setTimeout(() => {
                            window.print();
                            window.close();
                        }, 1000);
                    <\/script>
                </body>
            </html>
        `);
        printWindow.document.close();
    }

    // --- INIT ---
    document.addEventListener('DOMContentLoaded', () => {
        const statusFilter = document.getElementById('payStatusFilter');
        if (statusFilter) statusFilter.addEventListener('change', filterAndDisplayTable);
        
        const methodFilter = document.getElementById('payMethodFilter');
        if (methodFilter) methodFilter.addEventListener('change', filterAndDisplayTable);

        loadPayments();
    });

    window.loadPayments = loadPayments;

})();
