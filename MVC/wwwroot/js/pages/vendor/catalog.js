/**
 * Vendor Catalog Logic
 * Handles product grid, ElasticSearch integration, wishlist toggling, and cart management.
 */

(function () {
    const API_URL = 'http://localhost:5020/api/Vendor';
    let allProducts = [];
    let wishlistIds = [];
    let isSearchMode = false;
    let currentProduct = null;

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

    async function updateHeaderCartCount() {
        try {
            const response = await authorizedFetch(`${API_URL}/cart`);
            if (!response) return;
            const result = await response.json();
            const count = (result.success && result.data && result.data.items) ? result.data.items.length : 0;
            const headerCartSpan = document.getElementById('headerCartCount');
            if (headerCartSpan) headerCartSpan.innerText = count;
        } catch (e) {
            console.error('Cart count error:', e);
        }
    }

    function updateWishlistCount() {
        const el = document.getElementById('wishlistCount');
        if (el) el.innerText = wishlistIds.length;
        if (typeof syncGlobalBadges === 'function') syncGlobalBadges();
    }

    function escapeHtml(text) {
        return text ? text.replace(/[&<>]/g, m => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[m])) : '';
    }

    // ========== SEARCH LOGIC ==========
    async function elasticSearchProducts(searchQuery) {
        if (!searchQuery || searchQuery.trim() === '') {
            isSearchMode = false;
            const resInfo = document.getElementById('searchResultsInfo');
            if (resInfo) resInfo.style.display = 'none';
            await loadCatalogFromDatabase();
            return;
        }

        isSearchMode = true;
        const resInfo = document.getElementById('searchResultsInfo');
        const queryText = document.getElementById('searchQueryText');
        if (resInfo) resInfo.style.display = 'block';
        if (queryText) queryText.innerText = searchQuery;

        try {
            if (typeof window.FBSkeleton === 'object') {
                window.FBSkeleton.show('#catalogGrid', 4, 'product');
            }
            const response = await authorizedFetch(`${API_URL}/search/vendor-catalog`, {
                method: 'POST',
                body: JSON.stringify({ Query: searchQuery, Page: 1, PageSize: 100 })
            });
            
            if (!response || !response.ok) {
                allProducts = [];
                displayProducts([]);
                return;
            }

            const result = await response.json();
            if (result && result.results) {
                allProducts = result.results.map(p => ({
                    id: parseInt(p.id),
                    name: p.name,
                    category: p.category,
                    unit: p.unitOfMeasure || 'kg',
                    unitPrice: parseFloat(p.price) || 0,
                    quantityAvailable: parseFloat(p.quantityAvailable) || 0,
                    imageUrl: p.imageUrl,
                    grade: p.grade || ''
                }));
                await loadWishlistIds();
                applySortAndDisplay();
            } else {
                allProducts = [];
                displayProducts([]);
            }
        } catch (error) {
            console.error('Search error:', error);
            allProducts = [];
            displayProducts([]);
        }
    }

    function initCustomDropdowns() {
        $('.fb-dropdown').each(function() {
            const $dropdown = $(this);
            const $trigger = $dropdown.find('.fb-dropdown-trigger');
            const $select = $dropdown.find('select');

            $trigger.on('click', function(e) {
                e.stopPropagation();
                $('.fb-dropdown').not($dropdown).removeClass('open');
                $dropdown.toggleClass('open');
            });

            $dropdown.find('.fb-dropdown-item').on('click', function() {
                const val = $(this).data('value');
                const text = $(this).html();
                
                $dropdown.find('.fb-dropdown-item').removeClass('active');
                $(this).addClass('active');
                
                $trigger.find('span').html(text);
                $select.val(val).trigger('change');
                $dropdown.removeClass('open');
            });
        });

        $(document).on('click', function() {
            $('.fb-dropdown').removeClass('open');
        });
    }

    async function loadCatalogFromDatabase() {
        const categoryFilter = document.getElementById('categoryFilter');
        const category = categoryFilter ? categoryFilter.value : 'all';
        try {
            if (typeof window.FBSkeleton === 'object') {
                window.FBSkeleton.show('#catalogGrid', 8, 'product');
            }
            let url = `${API_URL}/catalog?category=${encodeURIComponent(category)}&search=`;
            const response = await authorizedFetch(url);
            if (!response) return;

            const result = await response.json();
            if (result.success && result.data) {
                allProducts = result.data.map(p => ({
                    ...p,
                    id: parseInt(p.id),
                    unitPrice: parseFloat(p.unitPrice) || 0,
                    quantityAvailable: parseFloat(p.quantityAvailable) || 0
                }));
                await loadWishlistIds();
                applySortAndDisplay();
            } else {
                document.getElementById('catalogGrid').innerHTML = '<div class="catalog-empty" style="text-align:center; padding: 40px;"><i class="fi fi-rr-box-open-full fa-3x mb-3"></i><p>No products found</p></div>';
            }
        } catch (error) {
            console.error('Database load error:', error);
        }
    }

    async function loadWishlistIds() {
        try {
            const response = await authorizedFetch(`${API_URL}/wishlist`);
            if (!response) return;
            const result = await response.json();
            if (result.success && result.data) {
                wishlistIds = result.data.map(item => `${item.productId}_${item.grade || 'nograde'}`);
                updateWishlistCount();
            }
        } catch (error) { wishlistIds = []; }
    }

    function applySortAndDisplay() {
        const sortFilter = document.getElementById('sortFilter');
        const sortBy = sortFilter ? sortFilter.value : 'default';
        let sortedProducts = [...allProducts];

        switch (sortBy) {
            case 'price-low': sortedProducts.sort((a, b) => (a.unitPrice || 0) - (b.unitPrice || 0)); break;
            case 'price-high': sortedProducts.sort((a, b) => (b.unitPrice || 0) - (a.unitPrice || 0)); break;
            case 'newest': sortedProducts.sort((a, b) => (b.id || 0) - (a.id || 0)); break;
        }
        displayProducts(sortedProducts);
    }

    function displayProducts(products) {
        const grid = document.getElementById('catalogGrid');
        if (!grid) return;
        if (!products || products.length === 0) {
            grid.innerHTML = '<div class="catalog-empty" style="text-align:center; padding: 40px;"><i class="fi fi-rr-box-open-full fa-3x mb-3"></i><p>No products found</p></div>';
            return;
        }

        let html = '<div class="products-container">';
        products.forEach(p => {
            const wishlistKey = `${p.id}_${p.grade || 'nograde'}`;
            const isInWishlist = wishlistIds.includes(wishlistKey);
            const price = p.unitPrice || 0;
            const img = p.imageUrl || null;
            const displayName = p.grade ? `${p.name} (Grade ${p.grade})` : p.name;
            const media = img ? `<img src="${img}" alt="${escapeHtml(displayName)}">` : `
                <div class="no-image-placeholder">
                    <div class="placeholder-icon-wrapper">
                        <i class="fi fi-rr-seedling"></i>
                        <div class="placeholder-text">FarmBridge</div>
                    </div>
                </div>`;

            html += `
            <div class="catalog-product-card">
                <div class="catalog-product-img">
                    ${p.grade ? `<div class="catalog-product-badge">Grade ${p.grade}</div>` : ''}
                    <div class="wishlist-heart-float ${isInWishlist ? 'active' : ''}" 
                         onclick="window.toggleWishlistItem(${p.id}, '${escapeHtml(p.name)}', ${price}, '${p.grade || ''}', event)">
                        <i class="${isInWishlist ? 'fi fi-sr-heart' : 'fi fi-rr-heart'}"></i>
                    </div>
                    ${media}
                </div>
                <div class="catalog-product-body">
                    <div class="catalog-product-name">${escapeHtml(displayName)}</div>
                    <div class="catalog-product-price">
                        ₹${price.toLocaleString('en-IN')}<small>/${p.unit || 'kg'}</small>
                    </div>
                    <div class="catalog-product-stock">
                        <i class="fi fi-rr-box"></i> ${p.quantityAvailable || 0} ${p.unit || 'kg'} <span data-i18n="cat_in_stock">in stock</span>
                    </div>
                    <button class="btn-catalog-add-cart" onclick='window.addToCart(${p.id}, "${escapeHtml(p.name)}", ${price}, ${p.quantityAvailable || 0}, "${img || ''}", "${p.grade || ''}")'>
                        <i class="fi fi-rr-shopping-cart-add"></i> <span data-i18n="cat_add_cart">Add to Cart</span>
                    </button>
                </div>
            </div>`;
        });
        html += '</div>';
        grid.innerHTML = html;
        if (window.fbInit) window.fbInit();
        updateHeaderCartCount();
    }

    async function toggleWishlistItem(productId, productName, price, grade, event) {
        event.stopPropagation();
        const btn = event.currentTarget;
        const isActive = btn.classList.contains('active');
        const wishlistKey = `${productId}_${grade || 'nograde'}`;

        try {
            let url = isActive
                ? `${API_URL}/wishlist/remove/${productId}?grade=${encodeURIComponent(grade || '')}`
                : `${API_URL}/wishlist/add/${productId}?grade=${encodeURIComponent(grade || '')}`;
            const response = await authorizedFetch(url, { method: isActive ? 'DELETE' : 'POST' });
            if (response?.ok) {
                btn.classList.toggle('active');
                btn.querySelector('i').className = btn.classList.contains('active') ? 'fi fi-sr-heart' : 'fi fi-rr-heart';
                if (isActive) wishlistIds = wishlistIds.filter(key => key !== wishlistKey);
                else wishlistIds.push(wishlistKey);
                updateWishlistCount();
            }
        } catch (error) { console.error('Wishlist error:', error); }
    }

    function addToCart(id, name, price, max, img, grade) {
        currentProduct = { 
            id: parseInt(id), 
            name: name, 
            price: parseFloat(price) || 0, 
            max: parseFloat(max) || 0, 
            grade: grade 
        };
        document.getElementById('popupProductName').innerText = grade ? `${name} (Grade ${grade})` : name;
        const availLabel = window.fbT ? window.fbT('cat_qty_avail') : 'Available';
        document.getElementById('popupStockInfo').innerText = `${availLabel}: ${currentProduct.max} kg`;
        
        const iconDiv = document.getElementById('popupProductIcon');
        iconDiv.innerHTML = img
            ? `<img src="${img}" style="width: 80px; height: 80px; border-radius: 16px; object-fit: cover; box-shadow: 0 8px 16px rgba(0,0,0,0.1);">`
            : `<div style="font-size: 40px;">🌾</div>`;

        const effectiveStep = currentProduct.max < 20 ? 1 : 5;
        const effectiveMin  = currentProduct.max < 20 ? 1 : 20;
        const popupInput = document.getElementById('popupQuantity');
        popupInput.step  = effectiveStep;
        popupInput.min   = effectiveMin;
        popupInput.value = currentProduct.max < 20 ? currentProduct.max : effectiveMin;
        updateTotalPrice();
        openQuantityWindow();
    }

    function updateTotalPrice() {
        const qty = parseFloat(document.getElementById('popupQuantity').value) || 0;
        const total = qty * (currentProduct?.price || 0);
        document.getElementById('popupTotalPrice').innerText = `₹${total.toLocaleString('en-IN', { maximumFractionDigits: 2, minimumFractionDigits: 2 })}`;
    }

    function openQuantityWindow() {
        if (!$("#quantityWindow").data("kendoWindow")) {
            $("#quantityWindow").kendoWindow({ 
                title: false, 
                modal: true, 
                visible: false, 
                width: 500,
                resizable: false,
                draggable: true,
                animation: {
                    open: { effects: "fade:in", duration: 300 },
                    close: { effects: "fade:out", duration: 200 }
                }
            });
        }
        $("#quantityWindow").data("kendoWindow").center().open();
    }

    async function confirmAddToCart() {
        if (!currentProduct) return;
        const input = document.getElementById('popupQuantity');
        const qty = parseInt(input.value) || 0;
        
        if (qty <= 0 || qty > currentProduct.max) {
            if (window.fbNotify) window.fbNotify({ message: `Please enter a valid quantity (max ${currentProduct.max} kg)`, icon: 'warning', type: 'toast' });
            return;
        }

        const btn = document.getElementById('confirmQtyBtn');
        if (btn) { btn.disabled = true; btn.innerHTML = '<i class="fi fi-rr-loading"></i> Adding...'; }

        try {
            const response = await authorizedFetch(`${API_URL}/cart/add`, {
                method: 'POST',
                body: JSON.stringify({ cropId: currentProduct.id, quantity: qty, grade: currentProduct.grade || '' })
            });
            if (response?.ok) {
                if (window.fbNotify) {
                    window.fbNotify({ message: "Added to cart successfully!", icon: "success", type: 'toast' });
                }
                closeCatalogModal();
                updateHeaderCartCount();
            } else {
                const err = await response?.json();
                if (window.fbNotify) window.fbNotify({ message: err?.message || "Failed to add to cart", icon: "error", type: 'toast' });
            }
        } catch (e) { 
            console.error(e); 
            if (window.fbNotify) window.fbNotify({ message: "Network error. Please try again.", icon: "error", type: 'toast' });
        } finally {
            if (btn) { btn.disabled = false; btn.innerHTML = '<i class="fi fi-rr-shopping-cart-add"></i> <span data-i18n="cat_qty_add">Add to Cart</span>'; }
        }
    }

    function closeCatalogModal() {
        const kw = $("#quantityWindow").data("kendoWindow");
        if (kw) kw.close();
    }

    function catalogStepQty(direction) {
        if (!currentProduct) return;
        const input = document.getElementById('popupQuantity');
        let val  = parseFloat(input.value) || 0;
        let step = currentProduct.max < 20 ? 1 : 5;
        let min  = currentProduct.max < 20 ? 1 : 20;
        let next = val + direction * step;
        if (next < min) next = min;
        if (next > currentProduct.max) next = currentProduct.max;
        input.value = next;
        updateTotalPrice();
    }

    function clampCatalogInput() {
        if (!currentProduct) return;
        const input = document.getElementById('popupQuantity');
        let val = parseFloat(input.value);
        let min = currentProduct.max < 20 ? 1 : 20;
        
        if (val > currentProduct.max) input.value = currentProduct.max;
        if (val < min && val !== 0) input.value = min;
        
        updateTotalPrice();
    }

    // --- INIT ---
    $(document).ready(function () {
        initCustomDropdowns();
        loadCatalogFromDatabase();

        $('#searchInput').on('input', _.debounce(function() {
            elasticSearchProducts($(this).val().trim());
        }, 300));

        $('#categoryFilter').on('change', () => { if (!isSearchMode) loadCatalogFromDatabase(); });
        $('#sortFilter').on('change', applySortAndDisplay);
        $('#clearSearch').on('click', () => { $('#searchInput').val(''); elasticSearchProducts(''); });
    });

    // Expose to window for inline onclicks
    window.addToCart = addToCart;
    window.confirmAddToCart = confirmAddToCart;
    window.closeCatalogModal = closeCatalogModal;
    window.catalogStepQty = catalogStepQty;
    window.clampCatalogInput = clampCatalogInput;
    window.toggleWishlistItem = toggleWishlistItem;

})();
