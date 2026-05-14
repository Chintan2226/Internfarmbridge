/**
 * Vendor Checkout Logic
 * Handles cart management, step navigation, address selection, and Razorpay integration.
 */

(function () {
    const API_URL = 'http://localhost:5020/api/Vendor';
    const MIN_QTY = 20;
    let cart = [];
    let currentStep = 1;
    let addresses = [];
    let selectedAddress = null;

    // --- HELPER FUNCTIONS ---
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    async function authorizedFetch(url, options = {}) {
        const token = getCookie("authToken");
        options.headers = {
            ...options.headers,
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
        };
        const response = await fetch(url, options);
        if (response.status === 401) {
            window.location.href = "/Vendor/Login";
            return null;
        }
        return response;
    }

    function escapeHtml(t) {
        if (!t) return '';
        return t.replace(/[&<>]/g, m => ({ '&':'&amp;','<':'&lt;','>':'&gt;' }[m]));
    }

    // --- TOAST NOTIFICATION ---
    function showToast(message, type = 'info') {
        let c = document.getElementById('fbToastContainer');
        if (!c) {
            c = document.createElement('div');
            c.id = 'fbToastContainer';
            c.className = 'fb-toast-container';
            document.body.appendChild(c);
        }
        const icons = { success:'✅', error:'❌', warning:'⚠️', info:'ℹ️' };
        const titles = { success:'Success!', error:'Error!', warning:'Warning!', info:'Info' };
        const t = document.createElement('div');
        t.className = `fb-toast ${type}`;
        t.innerHTML = `<div class="fb-toast-icon">${icons[type]}</div>
            <div class="fb-toast-content">
                <div class="fb-toast-title">${titles[type]}</div>
                <div class="fb-toast-message">${message}</div>
            </div>
            <button class="fb-toast-close" onclick="this.closest('.fb-toast').remove()">✕</button>`;
        c.appendChild(t);
        setTimeout(() => {
            t.classList.add('fade-out');
            setTimeout(() => t.remove(), 300);
        }, 3500);
    }

    // --- RENDER FUNCTIONS ---
    function renderCart() {
        const container = document.getElementById('cartContainer');
        const continueBtn = document.getElementById('continueToShipping');
        if (!container) return;

        if (!cart || cart.length === 0) {
            container.innerHTML = `
                <div class="text-center py-4">
                    <i class="fi fi-rr-shopping-cart-add empty-cart-icon"></i>
                    <h5 class="fw-bold text-dark mb-2">Your cart is empty</h5>
                    <p class="text-muted mb-3" style="font-size:13px;">Browse and add crops to get started.</p>
                    <a href="/Vendor/Catalog" class="btn-browse"><i class="fi fi-rr-search"></i> Browse Catalog</a>
                </div>`;
            if (continueBtn) continueBtn.style.display = 'none';
            updateTotals();
            return;
        }
        if (continueBtn) continueBtn.style.display = 'flex';

        let html = `<div class="table-responsive"><table class="cart-table">
            <thead><tr>
                <th>Product</th><th>Unit Price</th><th>Qty (kg)</th><th>Subtotal</th><th></th>
            </tr></thead><tbody>`;

        cart.forEach((item, idx) => {
            const minAllowed = Math.min(MIN_QTY, item.availableStock || MIN_QTY);
            const isMin = item.quantity <= minAllowed;
            const isMax = item.quantity >= (item.availableStock || item.quantity);
            html += `<tr>
                <td><div class="product-info-cell">
                    <span class="product-name">${escapeHtml(item.name)}</span>
                    ${item.grade ? `<span class="product-grade">${item.grade} Grade</span>` : ''}
                </div></td>
                <td><span class="fw-bold" style="font-size:13px;">₹${item.price.toLocaleString('en-IN')}</span></td>
                <td><div class="qty-wrapper">
                    <button class="qty-btn" onclick="window.updateQuantity(${idx},-1)" ${isMin?'disabled':''}>
                        <i class="fi fi-rr-minus-small"></i>
                    </button>
                    <input class="qty-input" type="text" value="${item.quantity}" readonly>
                    <button class="qty-btn" onclick="window.updateQuantity(${idx},1)" ${isMax?'disabled':''}>
                        <i class="fi fi-rr-plus-small"></i>
                    </button>
                </div></td>
                <td><span class="fw-bold" style="color:#10b981;font-size:13px;">₹${(item.price*item.quantity).toLocaleString('en-IN')}</span></td>
                <td><button class="remove-btn" onclick="window.removeItem(${idx})" title="Remove"><i class="fi fi-rr-trash"></i></button></td>
            </tr>`;
        });

        html += '</tbody></table></div>';
        container.innerHTML = html;
        updateTotals();
    }

    function renderAddresses() {
        const container = document.getElementById('addressList');
        if (!container) return;
        if (!addresses || addresses.length === 0) {
            container.innerHTML = `<div class="text-center py-4">
                <i class="fi fi-rr-marker" style="font-size:40px;color:#cbd5e1;"></i>
                <p class="mt-2 text-muted" style="font-size:13px;">No delivery addresses found.</p>
                <a href="/Vendor/Profile" class="btn-browse" style="margin-top:8px;"><i class="fi fi-rr-plus"></i> Add in Profile</a>
            </div>`;
            return;
        }
        container.innerHTML = addresses.map(addr => `
            <div class="address-card ${selectedAddress?.id === addr.id ? 'selected' : ''}" onclick="window.selectAddress(${addr.id})">
                <div class="form-check">
                    <input class="form-check-input" type="radio" name="address" ${selectedAddress?.id === addr.id ? 'checked' : ''}>
                    <label class="form-check-label">
                        <strong>${escapeHtml(addr.name)}</strong><br>
                        ${escapeHtml(addr.address)}, ${escapeHtml(addr.city)} - ${addr.pincode}
                        ${addr.isDefault ? '<span class="badge bg-success ms-2" style="font-size:10px;">Default</span>' : ''}
                    </label>
                </div>
            </div>`).join('');
    }

    function updateReviewOrder() {
        const container = document.getElementById('reviewOrderContainer');
        if (!container) return;
        if (cart.length === 0) {
            container.innerHTML = '<p class="text-center text-muted py-3">Your cart is empty.</p>';
            return;
        }

        let html = `<div class="table-responsive"><table class="cart-table">
            <thead><tr><th>Product</th><th>Qty</th><th>Unit Price</th><th>Total</th></tr></thead><tbody>`;
        cart.forEach(item => {
            html += `<tr>
                <td><div class="product-info-cell">
                    <span class="product-name">${escapeHtml(item.name)}</span>
                    ${item.grade ? `<span class="product-grade">${item.grade} Grade</span>` : ''}
                </div></td>
                <td class="fw-bold" style="font-size:13px;">${item.quantity} kg</td>
                <td class="text-muted" style="font-size:13px;">₹${item.price.toLocaleString('en-IN')}</td>
                <td><span class="fw-bold" style="color:#10b981;font-size:13px;">₹${(item.price*item.quantity).toLocaleString('en-IN')}</span></td>
            </tr>`;
        });
        html += '</tbody></table></div>';

        html += `<div class="row g-2 mt-2">
            <div class="col-6">
                <div style="background:#f8fafc;border-radius:12px;padding:12px;border:1px solid #e2e8f0;">
                    <div style="font-size:10px;font-weight:800;text-transform:uppercase;letter-spacing:.5px;color:#64748b;margin-bottom:6px;"><i class="fi fi-rr-marker me-1"></i>Delivery</div>
                    ${selectedAddress
                        ? `<div style="font-weight:700;font-size:13px;color:#14532d;">${escapeHtml(selectedAddress.name)}</div>
                           <div style="font-size:11px;color:#64748b;">${escapeHtml(selectedAddress.address)}, ${escapeHtml(selectedAddress.city)} - ${selectedAddress.pincode}</div>`
                        : '<div style="color:#ef4444;font-size:12px;">No address selected</div>'}
                </div>
            </div>
            <div class="col-6">
                <div style="background:#f8fafc;border-radius:12px;padding:12px;border:1px solid #e2e8f0;">
                    <div style="font-size:10px;font-weight:800;text-transform:uppercase;letter-spacing:.5px;color:#64748b;margin-bottom:6px;"><i class="fi fi-rr-credit-card me-1"></i>Payment</div>
                    <div style="font-weight:700;font-size:13px;color:#14532d;">Razorpay Secure</div>
                    <div style="font-size:11px;color:#64748b;">UPI, Cards & NetBanking</div>
                </div>
            </div>
        </div>`;

        container.innerHTML = html;
    }

    // --- ACTIONS ---
    window.selectAddress = function (id) {
        selectedAddress = addresses.find(a => a.id === id);
        renderAddresses();
    };

    window.updateQuantity = function (idx, change) {
        let newQty = cart[idx].quantity + change;
        let maxQty = cart[idx].availableStock || newQty;
        let minQty = Math.min(MIN_QTY, maxQty);
        
        if (newQty < minQty) { showToast(`Minimum quantity is ${minQty} kg`, 'warning'); return; }
        if (newQty > maxQty) { showToast(`Maximum available stock is ${maxQty} kg`, 'warning'); return; }
        
        cart[idx].quantity = newQty;
        renderCart();
    };

    window.removeItem = async function (idx) {
        const cartId = cart[idx].cartId;
        try {
            await authorizedFetch(`${API_URL}/cart/remove/${cartId}`, { method: 'DELETE' });
        } catch(e) {}
        cart.splice(idx, 1);
        renderCart();
        updateTotals();
    };

    function updateTotals() {
        const subtotal = cart.reduce((s,i) => s + i.price * i.quantity, 0);
        const delivery = subtotal > 5000 ? 0 : 100;
        const tax = Math.round(subtotal * 0.05);
        const total = subtotal + delivery + tax;
        const subEl = document.getElementById('subtotal');
        const delEl = document.getElementById('deliveryFee');
        const taxEl = document.getElementById('tax');
        const totEl = document.getElementById('total');
        
        if (subEl) subEl.innerText   = '₹' + subtotal.toLocaleString('en-IN');
        if (delEl) delEl.innerText = delivery === 0 ? 'FREE' : '₹' + delivery;
        if (taxEl) taxEl.innerText        = '₹' + tax.toLocaleString('en-IN');
        if (totEl) totEl.innerText      = '₹' + total.toLocaleString('en-IN');
    }

    window.goToStep = function (step) {
        if (step === 2 && cart.length === 0) { showToast('Your cart is empty!', 'warning'); return; }
        if (step === 3 && !selectedAddress)  { showToast('Please select a delivery address', 'warning'); return; }

        currentStep = step;

        document.querySelectorAll('.step').forEach((el, i) => {
            const n = i + 1;
            el.classList.remove('active', 'completed');
            if (n < step) el.classList.add('completed');
            else if (n === step) el.classList.add('active');
        });

        ['step1Content','step2Content','step3Content'].forEach((id, i) => {
            const el = document.getElementById(id);
            if (el) el.style.display = (i + 1 === step) ? 'flex' : 'none';
        });

        if (step === 2) renderAddresses();
        if (step === 3) updateReviewOrder();
    };

    // --- RAZORPAY INTEGRATION ---
    window.placeOrder = async function () {
        if (cart.length === 0)      { showToast('Your cart is empty!', 'warning'); return; }
        if (!selectedAddress)       { showToast('Please select a delivery address', 'warning'); window.goToStep(2); return; }

        const subtotal = cart.reduce((s,i) => s + i.price * i.quantity, 0);
        const delivery = subtotal > 5000 ? 0 : 100;
        const tax      = Math.round(subtotal * 0.05);
        const total    = subtotal + delivery + tax;

        const mappedPayment = 'razorpay';

        const orderData = {
            addressId: selectedAddress.id,
            paymentMethod: mappedPayment,
            totalAmount: total,
            items: cart.map(item => ({
                productId: item.id,
                productName: item.name,
                price: item.price,
                quantity: item.quantity,
                grade: item.grade
            }))
        };

        const btn = document.getElementById('placeOrderBtn');
        if (btn) {
            btn.disabled = true;
            btn.innerHTML = '<i class="fi fi-rr-spinner" style="animation:spin 0.8s linear infinite"></i> Processing...';
        }

        try {
            const orderRes = await fetch(`${API_URL}/checkout/place-order`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${getCookie('authToken')}` },
                body: JSON.stringify(orderData)
            });
            const orderResult = await orderRes.json();

            if (!orderResult.success) {
                showToast('Order creation failed: ' + orderResult.message, 'error');
                if (btn) { btn.disabled = false; btn.innerHTML = '<i class="fi fi-rr-lock"></i> Confirm & Pay'; }
                return;
            }

            const orderId = orderResult.orderId;
            const numericOrderId = parseInt(orderId.replace('FB-ORD-', ''));

            const rpRes = await fetch(`${API_URL}/payment/create-order`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${getCookie('authToken')}` },
                body: JSON.stringify({ orderId: numericOrderId, amount: total })
            });
            const rpData = await rpRes.json();

            if (!rpData.success) {
                showToast(rpData.message || 'Payment gateway error', 'error');
                if (btn) { btn.disabled = false; btn.innerHTML = '<i class="fi fi-rr-lock"></i> Confirm & Pay'; }
                return;
            }

            const options = {
                key: rpData.keyId,
                amount: rpData.amount * 100,
                currency: rpData.currency || 'INR',
                name: 'FarmBridge',
                description: `Order #${orderId}`,
                order_id: rpData.razorpayOrderId,
                handler: function(response) {
                    verifyPayment(numericOrderId, response, total);
                },
                prefill: { name: 'Vendor', email: 'vendor@farmbridge.com', contact: '9999999999' },
                theme: { color: '#10b981' },
                modal: {
                    ondismiss: function() {
                        showToast('Payment cancelled. Your order is on hold.', 'warning');
                        if (btn) { btn.disabled = false; btn.innerHTML = '<i class="fi fi-rr-lock"></i> Confirm & Pay'; }
                    }
                }
            };

            const rzp = new Razorpay(options);
            rzp.on('payment.failed', function(response) {
                showToast('Payment failed: ' + response.error.description, 'error');
                if (btn) { btn.disabled = false; btn.innerHTML = '<i class="fi fi-rr-lock"></i> Confirm & Pay'; }
            });
            rzp.open();

        } catch(err) {
            console.error('placeOrder error:', err);
            showToast('Network error. Please try again.', 'error');
            if (btn) { btn.disabled = false; btn.innerHTML = '<i class="fi fi-rr-lock"></i> Confirm & Pay'; }
        }
    };

    async function verifyPayment(orderId, rpResponse, amount) {
        try {
            showToast('Verifying payment...', 'info');
            const res = await fetch(`${API_URL}/payment/verify`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${getCookie('authToken')}` },
                body: JSON.stringify({
                    orderId: orderId,
                    razorpayOrderId: rpResponse.razorpay_order_id,
                    razorpayPaymentId: rpResponse.razorpay_payment_id,
                    razorpaySignature: rpResponse.razorpay_signature,
                    amount: amount
                })
            });
            const result = await res.json();
            if (result.success) {
                cart = [];
                showToast('Payment successful! Redirecting...', 'success');
                setTimeout(() => { window.location.href = '/Vendor/Orders'; }, 1800);
            } else {
                showToast('Verification failed: ' + result.message, 'error');
                const btn = document.getElementById('placeOrderBtn');
                if (btn) { btn.disabled = false; btn.innerHTML = '<i class="fi fi-rr-lock"></i> Confirm & Pay'; }
            }
        } catch(e) {
            showToast('Verification error. Contact support.', 'error');
        }
    }

    // --- DATA LOADERS ---
    async function loadCartFromAPI() {
        try {
            const res = await authorizedFetch(`${API_URL}/cart`);
            if (!res) return;
            const result = await res.json();
            if (result.success && result.data && result.data.items) {
                cart = result.data.items.map(item => ({
                    cartId: item.cartId,
                    id: item.cropId,
                    name: item.cropName,
                    price: parseFloat(item.unitPrice) || 0,
                    quantity: parseFloat(item.quantity) || 0,
                    availableStock: parseFloat(item.availableStock) || 0,
                    grade: item.grade || ''
                }));
            } else { cart = []; }
        } catch(e) { cart = []; }
    }

    async function loadAddressesFromAPI() {
        try {
            const res = await authorizedFetch(`${API_URL}/addresses`);
            if (!res) return;
            const result = await res.json();
            if (result.success && result.data && result.data.length > 0) {
                addresses = result.data.map(a => ({
                    id: a.id,
                    name: a.fullName || a.addressLine1?.split(',')[0] || 'Delivery',
                    address: a.addressLine1 || '',
                    city: a.city || '',
                    state: a.state || '',
                    pincode: a.zipCode || a.pincode || '',
                    isDefault: a.isDefault || false
                }));
                selectedAddress = addresses.find(a => a.isDefault) || addresses[0];
            } else { addresses = []; }
        } catch(e) { addresses = []; }
    }

    // --- INIT ---
    async function initCheckout() {
        await loadCartFromAPI();
        renderCart();
        await loadAddressesFromAPI();
    }

    $(document).ready(() => {
        initCheckout();
    });

})();
