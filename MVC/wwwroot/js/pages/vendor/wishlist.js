/**
 * Vendor Wishlist Logic
 * Handles wishlist rendering, item removal, and adding to cart.
 */

(function () {
    const API_URL = 'http://localhost:5020/api/Vendor';
    let wishlistItems = [];
    let currentWishlistProduct = null;

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

    async function updateCartCount() {
        try {
            const res = await authorizedFetch(`${API_URL}/cart`);
            if (!res) return;
            const result = await res.json();
            const count = result.success && result.data && result.data.items ? result.data.items.length : 0;
            const el = document.getElementById('headerCartCount');
            if (el) el.innerText = count;
        } catch(e) {}
    }

    function escapeHtml(t) {
        return t ? t.replace(/[&<>]/g, m => ({ '&':'&amp;','<':'&lt;','>':'&gt;' }[m])) : '';
    }

    // --- RENDER ---
    function renderWishlist() {
        const grid = document.getElementById('wishlistGrid');
        const clearBtn = document.getElementById('btnClearAll');
        if (!grid) return;

        if (!wishlistItems.length) {
            if (clearBtn) clearBtn.style.display = 'none';
            grid.innerHTML = `
                <div style="text-align:center;padding:80px 40px;background:rgba(255,255,255,.7);backdrop-filter:blur(16px);border-radius:24px;border:1px solid rgba(255,255,255,.5);box-shadow:0 10px 30px rgba(21,66,18,.06);">
                    <i class="fi fi-rr-heart" style="font-size:56px;color:#cbd5e1;display:block;margin-bottom:16px;"></i>
                    <h4 style="color:#14532d;font-size:22px;font-weight:700;margin-bottom:8px;" data-i18n="wish_empty">Your wishlist is empty</h4>
                    <p style="color:#4b5563;margin-bottom:24px;" data-i18n="wish_empty_desc">Save your favourite crops here and order them anytime</p>
                    <a href="/Vendor/Catalog" style="background:linear-gradient(135deg,#15803d,#166534);color:white;padding:12px 28px;border-radius:50px;font-weight:700;font-size:14px;display:inline-flex;align-items:center;gap:8px;transition:all .3s;text-decoration:none;">
                        <i class="fi fi-rr-seedling"></i> Browse Catalog
                    </a>
                </div>`;
            if (window.fbInit) window.fbInit();
            return;
        }

        if (clearBtn) { clearBtn.style.display = 'flex'; }

        let html = '<div class="products-container">';
        wishlistItems.forEach(item => {
            const img = item.imageUrl || null;
            const grade = item.grade || '';
            const name = item.productName || '';
            const displayName = grade ? `${name} (Grade ${grade})` : name;
            const price = item.avgPrice || 0;
            const stock = item.quantityAvailable || 0;
            const unit = item.unit || 'kg';

            const media = img
                ? `<img src="${img}" alt="${escapeHtml(displayName)}">`
                : `<div class="no-image-placeholder">
                       <div class="placeholder-icon-wrapper">
                           <i class="fi fi-rr-leaf"></i>
                           <div class="placeholder-text">FarmBridge</div>
                       </div>
                   </div>`;

            html += `
            <div class="catalog-product-card">
                <div class="catalog-product-img">
                    ${grade ? `<div class="catalog-product-badge">Grade ${grade}</div>` : ''}
                    <button class="wishlist-heart-float active" onclick="window.removeFromWishlist(${item.productId}, '${grade}')" title="Remove from Wishlist">
                        <i class="fi fi-sr-heart"></i>
                    </button>
                    ${media}
                </div>
                <div class="catalog-product-body">
                    <div class="catalog-product-name">${escapeHtml(displayName)}</div>
                    <div class="catalog-product-price">
                        ₹${price.toLocaleString('en-IN')}<small>/${unit}</small>
                    </div>
                    <div class="catalog-product-stock">
                        <i class="fi fi-rr-box"></i> ${stock.toLocaleString()} ${unit} in stock
                    </div>
                    <button class="btn-catalog-add-cart" onclick="window.openAddToCartModal(${item.productId}, '${escapeHtml(name)}', ${price}, ${stock}, '${img || ''}', '${grade}')">
                        <i class="fi fi-rr-shopping-cart-add"></i> Add to Cart
                    </button>
                </div>
            </div>`;
        });
        html += '</div>';
        grid.innerHTML = html;
        if (window.fbInit) window.fbInit();
    }

    // --- ACTIONS ---
    async function loadWishlist() {
        if (typeof window.FBSkeleton === 'object') window.FBSkeleton.show('#wishlistGrid', 6, 'product');
        try {
            const res = await authorizedFetch(`${API_URL}/wishlist`);
            if (!res) return;
            const result = await res.json();
            wishlistItems = (result.success && result.data) ? result.data : [];
        } catch(e) { wishlistItems = []; }
        renderWishlist();
    }

    window.removeFromWishlist = async function (productId, grade) {
        try {
            const gradeParam = grade ? `?grade=${encodeURIComponent(grade)}` : '';
            const res = await authorizedFetch(`${API_URL}/wishlist/remove/${productId}${gradeParam}`, { method: 'DELETE' });
            if (res?.ok) {
                wishlistItems = wishlistItems.filter(i => !(i.productId === productId && (i.grade || '') === (grade || '')));
                renderWishlist();
            }
        } catch(e) {}
    };

    window.clearAllWishlist = async function () {
        if (typeof Swal === 'undefined') return;
        const confirmed = await Swal.fire({
            title: 'Clear all wishlist items?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#10b981',
            cancelButtonColor: '#d33',
            confirmButtonText: 'Yes, Clear'
        }).then(r => r.isConfirmed);
        if (!confirmed) return;
        
        for (const item of wishlistItems) {
            const gradeParam = item.grade ? `?grade=${encodeURIComponent(item.grade)}` : '';
            await authorizedFetch(`${API_URL}/wishlist/remove/${item.productId}${gradeParam}`, { method: 'DELETE' });
        }
        wishlistItems = [];
        renderWishlist();
    };

    window.openAddToCartModal = function (productId, productName, price, maxQty, img, grade) {
        currentWishlistProduct = { id: productId, name: productName, price, max: maxQty, grade };
        document.getElementById('wishlistModalProductName').innerText = grade ? `${productName} (Grade ${grade})` : productName;
        document.getElementById('wishlistModalStock').innerHTML = `<span>Available</span>: ${maxQty} kg`;

        const iconDiv = document.getElementById('wishlistModalIcon');
        if (iconDiv) {
            iconDiv.innerHTML = img && img !== 'null'
                ? `<img src="${img}" style="width:80px;height:80px;border-radius:16px;object-fit:cover;box-shadow:0 8px 16px rgba(0,0,0,.1);">`
                : `<div style="font-size:40px;">🌾</div>`;
        }

        const effectiveStep = maxQty < 20 ? 1 : 5;
        const effectiveMin  = maxQty < 20 ? 1 : 20;
        const input = document.getElementById('wishlistPopupQuantity');
        if (input) {
            input.step  = effectiveStep;
            input.min   = effectiveMin;
            input.value = maxQty < 20 ? maxQty : effectiveMin;
        }
        updateModalPrice();

        const kwEl = $("#wishlistQuantityModal");
        if (!kwEl.data("kendoWindow")) {
            kwEl.kendoWindow({
                title: false, modal: true, visible: false, width: 500, resizable: false, draggable: true,
                animation: { open: { effects: "fade:in", duration: 300 }, close: { effects: "fade:out", duration: 200 } }
            });
        }
        kwEl.data("kendoWindow").center().open();
    };

    function updateModalPrice() {
        const input = document.getElementById('wishlistPopupQuantity');
        const priceEl = document.getElementById('wishlistModalTotalPrice');
        if (!input || !priceEl) return;
        const qty = parseFloat(input.value) || 0;
        const total = qty * (currentWishlistProduct?.price || 0);
        priceEl.innerText = `₹${total.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }

    window.clampWishlistInput = function () {
        if (!currentWishlistProduct) return;
        const input = document.getElementById('wishlistPopupQuantity');
        if (!input) return;
        let val = parseFloat(input.value);
        let min = currentWishlistProduct.max < 20 ? 1 : 20;
        
        if (val > currentWishlistProduct.max) input.value = currentWishlistProduct.max;
        if (val < min && val !== 0) input.value = min;
        
        updateModalPrice();
    };

    window.wishlistStepQty = function (direction) {
        if (!currentWishlistProduct) return;
        const input = document.getElementById('wishlistPopupQuantity');
        if (!input) return;
        let val  = parseFloat(input.value) || 0;
        let step = currentWishlistProduct.max < 20 ? 1 : 5;
        let min  = currentWishlistProduct.max < 20 ? 1 : 20;
        let next = val + direction * step;
        if (next < min) next = min;
        if (next > currentWishlistProduct.max) next = currentWishlistProduct.max;
        input.value = next;
        updateModalPrice();
    };

    window.closeWishlistModal = function () {
        const kw = $("#wishlistQuantityModal").data("kendoWindow");
        if (kw) kw.close();
    };

    window.confirmAddToCart = async function () {
        if (!currentWishlistProduct) return;
        const input = document.getElementById('wishlistPopupQuantity');
        const qty = parseFloat(input.value) || 0;

        if (qty <= 0 || qty > currentWishlistProduct.max) {
            if (window.fbNotify) window.fbNotify({ message: `Please enter a valid quantity (max ${currentWishlistProduct.max} kg)`, icon: 'warning', type: 'toast' });
            return;
        }

        const btn = document.getElementById('confirmWishlistQtyBtn');
        if (btn) { 
            btn.disabled = true; 
            btn.innerHTML = '<i class="fi fi-rr-loading"></i> Adding...'; 
        }

        try {
            const res = await authorizedFetch(`${API_URL}/cart/add`, {
                method: 'POST',
                body: JSON.stringify({ cropId: currentWishlistProduct.id, quantity: qty, grade: currentWishlistProduct.grade || '' })
            });
            if (res?.ok) {
                window.closeWishlistModal();
                updateCartCount();
                if (typeof window.syncGlobalBadges === 'function') window.syncGlobalBadges();
                if (window.fbNotify) window.fbNotify({ message: "Added to cart successfully!", icon: "success", type: 'toast' });
            } else {
                const err = await res?.json();
                if (window.fbNotify) window.fbNotify({ message: err?.message || "Failed to add to cart", icon: "error", type: 'toast' });
            }
        } catch(e) {
            if (window.fbNotify) window.fbNotify({ message: "Network error. Please try again.", icon: "error", type: 'toast' });
        } finally {
            if (btn) { 
                btn.disabled = false; 
                btn.innerHTML = '<i class="fi fi-rr-shopping-cart-add"></i> <span>Add to Cart</span>'; 
            }
        }
    };

    // --- INIT ---
    $(document).ready(() => {
        loadWishlist();
        updateCartCount();
    });

})();
