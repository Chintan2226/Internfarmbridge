/* ========== FARMBRIDGE GLOBAL SKELETON HELPER ========== */

const FarmBridgeSkeleton = {
    /**
     * Injects skeletons into a container.
     * @param {string} containerId - The selector of the container.
     * @param {number} count - Number of items.
     * @param {string} template - 'product', 'order', 'kpi', 'chart', 'list'
     */
    show: function(containerId, count = 6, template = 'product') {
        const container = $(containerId);
        if (!container.length) return;

        let itemsHtml = '';
        
        // Determine the best grid wrapper for the template
        let wrapperClass = (template === 'product' || template === 'order') ? 'fb-skeleton-grid-cards' : '';
        
        // Use global grid class if the specific catalog/wishlist one isn't needed/available
        if (template === 'order' && !$('.fb-skeleton-grid-cards').length) {
            wrapperClass = 'fbs-grid';
        }

        for (let i = 0; i < count; i++) {
            switch(template) {
                case 'product':
                    // Restore original structure for catalog/wishlist compatibility
                    itemsHtml += `
                        <div class="fb-skeleton-card">
                            <div class="skeleton-body">
                                <div class="fb-skeleton-line title"></div>
                                <div class="fb-skeleton-line"></div>
                                <div class="fb-skeleton-line price"></div>
                                <div class="fb-skeleton-line btn"></div>
                            </div>
                        </div>`;
                    break;
                case 'order':
                    itemsHtml += `
                        <div class="fbs-card">
                            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; padding-bottom: 12px; border-bottom: 1px dashed #e2e8f0;">
                                <div class="fb-skeleton fb-skeleton-text" style="width: 30%; margin: 0;"></div>
                                <div class="fb-skeleton fb-skeleton-pill" style="width: 80px; height: 24px;"></div>
                            </div>
                            <div style="display: flex; gap: 12px;">
                                <div class="fb-skeleton fb-skeleton-text" style="flex: 1; height: 50px; border-radius: 12px;"></div>
                                <div class="fb-skeleton fb-skeleton-text" style="flex: 1; height: 50px; border-radius: 12px;"></div>
                            </div>
                            <div class="fb-skeleton fb-skeleton-text" style="width: 100%; height: 70px; border-radius: 16px; margin-top: 8px;"></div>
                            <div style="display: flex; gap: 10px; margin-top: auto;">
                                <div class="fb-skeleton fb-skeleton-btn" style="flex: 1"></div>
                                <div class="fb-skeleton fb-skeleton-btn" style="width: 48px;"></div>
                            </div>
                        </div>`;
                    break;
                case 'kpi':
                    itemsHtml += `
                        <div class="fbs-card" style="padding: 24px; min-height: 140px;">
                            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
                                <div class="fb-skeleton fb-skeleton-text" style="width: 50%; height: 12px;"></div>
                                <div class="fb-skeleton fb-skeleton-circle" style="width: 44px; height: 44px; border-radius: 14px;"></div>
                            </div>
                            <div class="fb-skeleton fb-skeleton-title" style="width: 70%; height: 32px; margin-bottom: 12px;"></div>
                            <div class="fb-skeleton fb-skeleton-text" style="width: 90%; height: 10px;"></div>
                        </div>`;
                    break;
                case 'list':
                    itemsHtml += `
                        <div class="fb-skeleton-list-item">
                            <div class="fb-skeleton fb-skeleton-circle"></div>
                            <div style="flex: 1">
                                <div class="fb-skeleton fb-skeleton-title" style="width: 40%"></div>
                                <div class="fb-skeleton fb-skeleton-text" style="width: 80%"></div>
                            </div>
                            <div class="fb-skeleton fb-skeleton-pill" style="width: 60px"></div>
                        </div>`;
                    break;
                case 'chart':
                    itemsHtml = `
                        <div class="fbs-card" style="width: 100%; height: 100%; min-height: 250px;">
                            <div class="fb-skeleton fb-skeleton-title" style="width: 30%"></div>
                            <div class="fb-skeleton fb-skeleton-text" style="width: 20%; margin-bottom: 24px;"></div>
                            <div style="display: flex; align-items: flex-end; gap: 12px; flex: 1; padding-top: 20px;">
                                <div class="fb-skeleton" style="flex: 1; height: 40%;"></div>
                                <div class="fb-skeleton" style="flex: 1; height: 70%;"></div>
                                <div class="fb-skeleton" style="flex: 1; height: 50%;"></div>
                                <div class="fb-skeleton" style="flex: 1; height: 90%;"></div>
                                <div class="fb-skeleton" style="flex: 1; height: 60%;"></div>
                                <div class="fb-skeleton" style="flex: 1; height: 80%;"></div>
                            </div>
                        </div>`;
                    break;
            }
        }

        // Smart wrapping: only wrap if the container itself is NOT a grid
        const isContainerGrid = container.css('display') === 'grid' || 
                               container.hasClass('orders-grid') || 
                               container.hasClass('products-container') ||
                               container.hasClass('fb-skeleton-grid-cards');

        if (wrapperClass && !isContainerGrid) {
            container.html(`<div class="${wrapperClass}">${itemsHtml}</div>`);
        } else {
            container.html(itemsHtml);
        }
        
        container.show();
    },

    /**
     * Shows inline text skeletons for specific elements.
     * @param {string} selector - jQuery selector
     */
    showInline: function(selector) {
        $(selector).each(function() {
            $(this).addClass('fb-skeleton-text-inline');
        });
    },

    hide: function(containerId) {
        $(containerId).empty();
    }
};

window.FBSkeleton = FarmBridgeSkeleton;
