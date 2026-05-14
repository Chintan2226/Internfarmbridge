/**
 * Field Officer Management Logic
 * Handles officer directory, profile modal, and status updates.
 */

(function () {
    'use strict';

    const API_BASE = window.API_BASE || 'http://localhost:5020/api/Admin';

    /* -- State -- */
    var allOfficers = [];
    var filteredOfficers = [];
    var currentFoPage = 1;
    var foPageSize = 9;
    var totalFoPages = 1;
    var currentFoView = 'card';
    var currentFoId = null;

    window.currentFoPage = currentFoPage;
    window.totalFoPages = totalFoPages;

    /* == INIT == */
    document.addEventListener('DOMContentLoaded', function () { loadOfficers(); });

    async function loadOfficers() {
        if (typeof FBSkeleton !== 'undefined') {
            FBSkeleton.show('#fo-cards-container', 9, 'farmer');
        }
        if (typeof window.showGridSkeleton === 'function') {
            window.showGridSkeleton('#fo-tbody', 10);
        }
        setBanner('loading', '⏳ Connecting to API...');
        try {
            var res = await authFetch(API_BASE + '/field-officers');

            if (res.status === 401) {
                setBanner('error', '🔒 Session expired. Please login again.');
                allOfficers = [];
            } else if (!res.ok) {
                var text = await res.text();
                setBanner('error', '❌ Error ' + res.status);
                allOfficers = [];
            } else {
                var json = await res.json();
                allOfficers = json.data || json.items || (Array.isArray(json) ? json : []);
                allOfficers = allOfficers.map(normaliseOfficer);
                hideBanner();
                foShowToast('✅ Loaded ' + allOfficers.length + ' field officers');
            }
        } catch (err) {
            setBanner('error', '🔗 Connection error.');
            console.error('[FO] loadOfficers:', err);
            allOfficers = [];
        }

        filteredOfficers = allOfficers.slice();
        foPopulateRegionFilter();
        foUpdateStats();
        foRender();
    }

    function normaliseOfficer(o) {
        return {
            id: o.id ?? o.Id,
            foCode: o.foCode ?? o.FoCode ?? ('FO-' + (o.id ?? o.Id)),
            fullName: o.fullName ?? o.FullName ?? '',
            email: o.email ?? o.Email ?? '',
            phone: o.phone ?? o.Phone ?? '',
            assignedRegion: o.assignedRegion ?? o.AssignedRegion ?? '',
            warehouseName: o.warehouseName ?? o.WarehouseName ?? '',
            warehouseId: o.warehouseId ?? o.WarehouseId,
            inspectionCount: o.inspectionCount ?? o.InspectionCount ?? o.totalInspections ?? o.TotalInspections ?? 0,
            passedCount: o.passedCount ?? o.PassedCount ?? 0,
            lotsManaged: o.lotsManaged ?? o.LotsManaged ?? 0,
            isActive: o.isActive ?? o.IsActive ?? false,
            createdAt: o.createdAt ?? o.CreatedAt ?? null
        };
    }

    /* == BANNER == */
    function setBanner(type, msg) {
        var b = document.getElementById('fo-api-banner');
        if (!b) return;
        b.className = 'api-banner ' + type;
        var m = document.getElementById('fo-api-banner-msg');
        if (m) m.innerHTML = msg;
    }
    function hideBanner() {
        var b = document.getElementById('fo-api-banner');
        if (b) b.className = 'api-banner';
    }

    /* == STATS == */
    function foUpdateStats() {
        var total = allOfficers.length;
        var active = allOfficers.filter(function (o) { return o.isActive; }).length;
        var warehouses = new Set(allOfficers.map(function (o) { return o.warehouseId; }).filter(Boolean)).size;
        var inspections = allOfficers.reduce(function (s, o) { return s + (o.inspectionCount || 0); }, 0);

        const elTotal = document.getElementById('stat-total');
        if (elTotal) elTotal.textContent = total;

        const elActive = document.getElementById('stat-active');
        if (elActive) elActive.textContent = active;

        const elWH = document.getElementById('stat-warehouses');
        if (elWH) elWH.textContent = warehouses;

        const elInsp = document.getElementById('stat-inspections');
        if (elInsp) elInsp.textContent = inspections;

        const elTNote = document.getElementById('stat-total-note');
        if (elTNote) elTNote.textContent = total + ' registered';

        const elANote = document.getElementById('stat-active-note');
        if (elANote) elANote.textContent = total > 0 ? Math.round((active / total) * 100) + '% active rate' : '—';

        const elWNote = document.getElementById('stat-warehouse-note');
        if (elWNote) elWNote.textContent = 'Across ' + warehouses + ' locations';
    }

    /* == FILTERS == */
    function foPopulateRegionFilter() {
        var regions = [...new Set(allOfficers.map(function (o) { return o.assignedRegion; }).filter(Boolean))].sort();
        var sel = document.getElementById('fo-region-filter');
        if (!sel) return;
        sel.innerHTML = '<option value="">All Regions</option>';
        regions.forEach(function (r) { var op = document.createElement('option'); op.value = r; op.textContent = r; sel.appendChild(op); });
    }

    window.foFilterData = function () {
        var q = document.getElementById('fo-search').value.toLowerCase();
        var status = document.getElementById('fo-status-filter').value;
        var region = document.getElementById('fo-region-filter').value;
        filteredOfficers = allOfficers.filter(function (o) {
            var mq = !q || (o.fullName + o.assignedRegion + o.warehouseName).toLowerCase().includes(q);
            var ms = !status || (status === 'active' ? o.isActive : !o.isActive);
            var mr = !region || o.assignedRegion === region;
            return mq && ms && mr;
        });
        currentFoPage = 1;
        foRender();
    };

    window.foClearFilters = function () {
        document.getElementById('fo-search').value = '';
        document.getElementById('fo-status-filter').value = '';
        document.getElementById('fo-region-filter').value = '';
        filteredOfficers = allOfficers.slice();
        currentFoPage = 1;
        foRender();
    };

    /* == VIEW == */
    window.foSetView = function (v) {
        currentFoView = v;
        foPageSize = v === 'card' ? 9 : 25;
        currentFoPage = 1;
        const elCard = document.getElementById('fo-card-view');
        const elTable = document.getElementById('fo-table-view');
        if (elCard) elCard.style.display = v === 'card' ? 'block' : 'none';
        if (elTable) elTable.style.display = v === 'table' ? 'block' : 'none';

        const btnCard = document.getElementById('btn-card-view');
        const btnTable = document.getElementById('btn-table-view');
        if (btnCard) btnCard.className = 'view-btn' + (v === 'card' ? ' active' : '');
        if (btnTable) btnTable.className = 'view-btn' + (v === 'table' ? ' active' : '');
        foRender();
    };

    /* == RENDER == */
    function foRender() {
        totalFoPages = Math.max(1, Math.ceil(filteredOfficers.length / foPageSize));
        window.totalFoPages = totalFoPages;
        window.currentFoPage = currentFoPage;

        var start = (currentFoPage - 1) * foPageSize;
        var slice = filteredOfficers.slice(start, start + foPageSize);

        const elCount = document.getElementById('fo-result-count');
        if (elCount) elCount.textContent = 'Showing ' + filteredOfficers.length + ' of ' + allOfficers.length + ' officers';

        if (currentFoView === 'card') foRenderCards(slice);
        else foRenderTable(slice);
        foRenderPagination(start);
    }

    function foRenderCards(slice) {
        var c = document.getElementById('fo-cards-container');
        if (!c) return;
        if (!slice.length) { c.innerHTML = '<div class="empty-state" style="grid-column:1/-1">No field officers found</div>'; return; }
        c.innerHTML = slice.map(function (o) {
            return '<div class="fo-card">' +
                '<div class="fo-card-header">' +
                '<div class="fo-avatar">' + foGetInitials(o.fullName) + '</div>' +
                '<div style="min-width:0"><div class="fo-card-name">' + foEsc(o.fullName) + '</div><div class="fo-card-id">#' + foEsc(o.foCode) + '</div></div>' +
                '<div class="fo-card-status">' + foGetPill(o.isActive) + '</div>' +
                '</div>' +
                '<div class="fo-card-body">' +
                '<div class="fo-info-row"><div class="fo-info-icon"><svg fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z"/></svg></div><div><div class="fo-info-label">Phone</div><div class="fo-info-val">' + foEsc(o.phone || '—') + '</div></div></div>' +
                '<div class="fo-info-row"><div class="fo-info-icon"><svg fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/></svg></div><div><div class="fo-info-label">Assigned Region</div><div class="fo-info-val">' + foEsc(o.assignedRegion || '—') + '</div></div></div>' +
                '<div class="fo-info-row"><div class="fo-info-icon"><svg fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4"/></svg></div><div><div class="fo-info-label">Warehouse</div><div class="fo-info-val">' + foEsc(o.warehouseName || '—') + '</div></div></div>' +
                '</div>' +
                '<div class="fo-card-footer">' +
                '<button class="fo-btn fo-btn-sm fo-btn-view" onclick="foOpenModal(' + o.id + ')">View Details</button>' +
                (o.isActive
                    ? '<button class="fo-btn fo-btn-sm fo-btn-deactivate" onclick="foQuickStatus(' + o.id + ',false)">Deactivate</button>'
                    : '<button class="fo-btn fo-btn-sm fo-btn-activate" onclick="foQuickStatus(' + o.id + ',true)">Activate</button>') +
                '</div>' +
                '</div>';
        }).join('');
    }

    function foRenderTable(slice) {
        var tbody = document.getElementById('fo-tbody');
        if (!tbody) return;
        const elBadge = document.getElementById('t-badge');
        if (elBadge) elBadge.textContent = filteredOfficers.length + ' officers';
        if (!slice.length) { tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;padding:32px;color:var(--text-light)">No field officers found</td></tr>'; return; }
        tbody.innerHTML = slice.map(function (o) {
            return '<tr>' +
                '<td><div class="fo-cell"><div class="avatar-sm">' + foGetInitials(o.fullName) + '</div><div><div class="fo-name">' + foEsc(o.fullName) + '</div><div class="fo-code">#' + foEsc(o.foCode) + '</div></div></div></td>' +
                '<td>' + foEsc(o.phone || '—') + '</td>' +
                '<td>' + foEsc(o.assignedRegion || '—') + '</td>' +
                '<td>' + foEsc(o.warehouseName || '—') + '</td>' +
                '<td>' + foFormatDate(o.createdAt) + '</td>' +
                '<td>' + foGetPill(o.isActive) + '</td>' +
                '<td><div class="actions-cell">' +
                '<button class="fo-btn fo-btn-sm fo-btn-view" onclick="foOpenModal(' + o.id + ')">View</button>' +
                (o.isActive
                    ? '<button class="fo-btn fo-btn-sm fo-btn-deactivate" onclick="foQuickStatus(' + o.id + ',false)">Deactivate</button>'
                    : '<button class="fo-btn fo-btn-sm fo-btn-activate" onclick="foQuickStatus(' + o.id + ',true)">Activate</button>') +
                '</div></td>' +
                '</tr>';
        }).join('');
    }

    function foRenderPagination(start) {
        var p = currentFoView === 'card' ? 'c' : 't';
        var numsEl = document.getElementById(p + '-pg-nums');
        var totalEl = document.getElementById(p + '-pg-total');
        if (numsEl) {
            var html = '';
            var maxP = Math.min(totalFoPages, 5);
            for (var i = 1; i <= maxP; i++) html += '<button class="pg-btn' + (i === currentFoPage ? ' active' : '') + '" onclick="foGoPage(' + i + ')">' + i + '</button>';
            numsEl.innerHTML = html;
        }
        if (totalEl) totalEl.textContent = filteredOfficers.length > 0
            ? 'Showing ' + (start + 1) + '–' + Math.min(start + foPageSize, filteredOfficers.length) + ' of ' + filteredOfficers.length
            : '';
        ['first', 'prev', 'next', 'last'].forEach(function (t) {
            var el = document.getElementById(p + '-pg-' + t);
            if (el) el.disabled = (t === 'first' || t === 'prev') ? currentFoPage === 1 : currentFoPage === totalFoPages;
        });
    }

    window.foGoPage = function (pg) { if (pg < 1 || pg > totalFoPages) return; currentFoPage = pg; window.currentFoPage = pg; foRender(); };
    window.foChangePageSize = function (v) { foPageSize = parseInt(v); currentFoPage = 1; foRender(); };

    /* == MODAL — open == */
    window.foOpenModal = function (id) {
        currentFoId = id;
        var o = allOfficers.find(function (x) { return x.id === id; });
        if (!o) return;

        const elTitle = document.getElementById('fo-modal-title');
        if (elTitle) elTitle.textContent = 'Field Officer — ' + o.fullName;

        const elMAvatar = document.getElementById('m-avatar');
        if (elMAvatar) elMAvatar.textContent = foGetInitials(o.fullName);

        const elMName = document.getElementById('m-name');
        if (elMName) elMName.textContent = o.fullName || '—';

        const elMMeta = document.getElementById('m-meta');
        if (elMMeta) elMMeta.textContent = '#' + o.foCode + ' · Joined ' + foFormatDate(o.createdAt);

        const elMStatusBadge = document.getElementById('m-status-badge');
        if (elMStatusBadge) elMStatusBadge.innerHTML = foGetPill(o.isActive);

        const elMFullName = document.getElementById('m-fullname');
        if (elMFullName) elMFullName.textContent = o.fullName || '—';

        const elMPhone = document.getElementById('m-phone');
        if (elMPhone) elMPhone.textContent = o.phone || '—';

        const elMEmail = document.getElementById('m-email');
        if (elMEmail) elMEmail.textContent = o.email || '—';

        const elMRegion = document.getElementById('m-region');
        if (elMRegion) elMRegion.textContent = o.assignedRegion || '—';

        const elMWarehouse = document.getElementById('m-warehouse');
        if (elMWarehouse) elMWarehouse.textContent = o.warehouseName || '—';

        const elMJoined = document.getElementById('m-joined');
        if (elMJoined) elMJoined.textContent = foFormatDate(o.createdAt);

        const elMAccStatus = document.getElementById('m-acc-status');
        if (elMAccStatus) elMAccStatus.textContent = o.isActive ? 'Active' : 'Inactive';

        const elMPInsp = document.getElementById('m-perf-inspections');
        if (elMPInsp) elMPInsp.textContent = o.inspectionCount || 0;

        const elMPPass = document.getElementById('m-perf-passed');
        if (elMPPass) elMPPass.textContent = o.passedCount || 0;

        const elMPLots = document.getElementById('m-perf-lots');
        if (elMPLots) elMPLots.textContent = o.lotsManaged || 0;

        foSwitchTabByName('tab-profile');
        const elModalOverlay = document.getElementById('fo-modal-overlay');
        if (elModalOverlay) elModalOverlay.classList.add('open');

        foLoadDetail(id);
        foLoadInspections(id);
        foLoadSlots(id);
    };

    async function foLoadDetail(id) {
        try {
            var res = await authFetch(API_BASE + '/FoDetail/' + id);
            if (!res.ok) return;
            var json = await res.json();
            if (!json.success || !json.data) return;
            var d = json.data;

            const elMPInsp = document.getElementById('m-perf-inspections');
            if (elMPInsp) elMPInsp.textContent = d.totalInspections ?? 0;

            const elMPPass = document.getElementById('m-perf-passed');
            if (elMPPass) elMPPass.textContent = d.passedInspections ?? 0;

            const elMPLots = document.getElementById('m-perf-lots');
            if (elMPLots) elMPLots.textContent = d.lotsManaged ?? 0;
        } catch (e) {
            console.warn('[FO] foLoadDetail:', e);
        }
    }

    async function foLoadInspections(id) {
        var el = document.getElementById('inspections-content');
        if (!el) return;
        if (typeof window.showGridSkeleton === 'function') {
            window.showGridSkeleton(el, 3);
        }
        try {
            var res = await authFetch(API_BASE + '/FoInspections/' + id);
            if (!res.ok) throw new Error('HTTP ' + res.status);
            var json = await res.json();

            const elTabInspCount = document.getElementById('tab-insp-count');
            if (elTabInspCount) elTabInspCount.textContent = json.total || 0;

            var rows = json.data || [];
            if (!rows.length) {
                el.innerHTML = '<div class="empty-state">No inspections recorded</div>';
                return;
            }

            el.innerHTML =
                '<table class="mini-table">' +
                '<thead><tr>' +
                '<th>Insp. ID</th><th>Crop</th><th>Farmer</th>' +
                '<th>Grade</th><th>Weight (kg)</th><th>Accepted</th>' +
                '<th>Passed</th><th>Submitted</th>' +
                '</tr></thead><tbody>' +
                rows.map(function (d) {
                    var gradeKey = ((d.grade || d.Grade || '').toUpperCase().charAt(0) || 'A');
                    return '<tr>' +
                        '<td>#' + (d.inspectionId || d.InspectionId) + '</td>' +
                        '<td>' + foEsc(d.cropName || d.CropName || '—') +
                        (d.variety || d.Variety ? ' <small style="color:var(--text-muted)">(' + foEsc(d.variety || d.Variety) + ')</small>' : '') +
                        '</td>' +
                        '<td>' + foEsc(d.farmerName || d.FarmerName || '—') + '</td>' +
                        '<td><span class="grade-badge grade-' + gradeKey + '">' + foEsc(d.grade || d.Grade || '—') + '</span></td>' +
                        '<td>' + (d.weightCheckedKg ?? d.WeightCheckedKg ?? '—') + '</td>' +
                        '<td>' + (d.acceptedQuantity ?? d.AcceptedQuantity ?? '—') + ' kg</td>' +
                        '<td>' + ((d.passed ?? d.Passed)
                            ? '<span class="status-pill pill-active"><span class="pill-dot"></span>Yes</span>'
                            : '<span class="status-pill pill-inactive"><span class="pill-dot"></span>No</span>') +
                        '</td>' +
                        '<td>' + foFormatDate(d.submittedAt || d.SubmittedAt) + '</td>' +
                        '</tr>';
                }).join('') +
                '</tbody></table>';
        } catch (e) {
            el.innerHTML = '<div class="empty-state">Could not load inspections.</div>';
            console.warn('[FO] foLoadInspections:', e);
        }
    }

    async function foLoadSlots(id) {
        var el = document.getElementById('slots-content');
        if (!el) return;
        if (typeof window.showGridSkeleton === 'function') {
            window.showGridSkeleton(el, 3);
        }
        try {
            var res = await authFetch(API_BASE + '/FoWarehouseSlots/' + id);
            if (!res.ok) throw new Error('HTTP ' + res.status);
            var json = await res.json();

            const elTabSlotsCount = document.getElementById('tab-slots-count');
            if (elTabSlotsCount) elTabSlotsCount.textContent = json.total || 0;

            var rows = json.data || [];
            if (!rows.length) {
                el.innerHTML = '<div class="empty-state">No warehouse slots found</div>';
                return;
            }

            el.innerHTML =
                '<table class="mini-table">' +
                '<thead><tr>' +
                '<th>Farmer</th><th>Crop</th><th>Slot Date</th>' +
                '<th>Time</th><th>Warehouse</th>' +
                '<th>Booking</th><th>Check-In</th><th>Lot</th>' +
                '</tr></thead><tbody>' +
                rows.map(function (s) {
                    var lotCell = (s.lotId || s.LotId)
                        ? '<span class="status-pill pill-active" style="font-size:10px">' +
                        'Grade ' + foEsc(s.lotGrade || s.LotGrade || '—') +
                        ' · ' + (s.quantityAccepted ?? s.QuantityAccepted ?? 0) + ' kg' +
                        '</span>'
                        : '<span style="color:var(--text-light);font-size:12px">—</span>';

                    return '<tr>' +
                        '<td>' + foEsc(s.farmerName || s.FarmerName || '—') + '</td>' +
                        '<td>' + foEsc(s.cropName || s.CropName || '—') + '</td>' +
                        '<td>' + foFormatDate(s.slotDate || s.SlotDate) + '</td>' +
                        '<td style="white-space:nowrap">' +
                        foEsc((s.slotTimeStart || s.SlotTimeStart || '—') + ' – ' + (s.slotTimeEnd || s.SlotTimeEnd || '—')) +
                        '</td>' +
                        '<td>' + foEsc(s.warehouseName || s.WarehouseName || '—') + '</td>' +
                        '<td>' + foEsc(s.bookingStatus || s.BookingStatus || '—') + '</td>' +
                        '<td>' + foEsc((s.checkInStatus || s.CheckInStatus || '—').replace(/_/g, ' ')) + '</td>' +
                        '<td>' + lotCell + '</td>' +
                        '</tr>';
                }).join('') +
                '</tbody></table>';
        } catch (e) {
            el.innerHTML = '<div class="empty-state">Could not load slots.</div>';
            console.warn('[FO] foLoadSlots:', e);
        }
    }

    /* == MODAL — close / tabs == */
    window.foCloseModal = function () {
        const el = document.getElementById('fo-modal-overlay');
        if (el) el.classList.remove('open');
        currentFoId = null;
    };
    window.foCloseModalOnOverlay = function (e) { if (e.target === document.getElementById('fo-modal-overlay')) foCloseModal(); };

    window.foSwitchTab = function (el, id) {
        document.querySelectorAll('.fo-tab-item').forEach(function (t) { t.classList.remove('active'); });
        el.classList.add('active');
        foSwitchTabByName(id);
    };
    function foSwitchTabByName(id) {
        ['tab-profile', 'tab-inspections', 'tab-slots'].forEach(function (t) {
            const el = document.getElementById(t);
            if (el) el.style.display = t === id ? 'block' : 'none';
        });
    }

    /* == STATUS TOGGLE == */
    window.foChangeStatus = async function (active) { if (!currentFoId) return; await foQuickStatus(currentFoId, active); foCloseModal(); };

    window.foQuickStatus = async function (id, active) {
        try {
            var res = await authFetch(API_BASE + '/' + id + '/update-status', {
                method: 'PATCH',
                body: JSON.stringify({ isActive: active })
            });

            if (!res.ok) throw new Error('HTTP ' + res.status);

            var o = allOfficers.find(function (x) { return x.id === id; });
            if (o) o.isActive = active;
            filteredOfficers = filteredOfficers.map(function (x) { return x.id === id ? Object.assign({}, x, { isActive: active }) : x; });

            foUpdateStats();
            foRender();
            foShowToast('Officer ' + (active ? 'activated' : 'deactivated') + ' successfully');
        } catch (e) {
            foShowToast('Update failed.', true);
        }
    };

    /* == EXPORT / AUDIT == */
    window.foExportCSV = function () {
        var headers = ['Code', 'Full Name', 'Phone', 'Email', 'Region', 'Warehouse', 'Status', 'Joined'];
        var rows = filteredOfficers.map(function (o) {
            return [o.foCode, o.fullName, o.phone, o.email, o.assignedRegion, o.warehouseName,
            o.isActive ? 'Active' : 'Inactive', foFormatDate(o.createdAt)]
                .map(function (c) { return '"' + String(c || '').replace(/"/g, '""') + '"'; }).join(',');
        });
        var csv = [headers.join(',')].concat(rows).join('\n');
        var a = document.createElement('a');
        a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
        a.download = 'field_officers_' + new Date().toISOString().slice(0, 10) + '.csv';
        a.click();
        foShowToast('CSV exported successfully');
    };

    window.foLoadAuditLog = function () { foShowToast('Audit log module triggered'); };

    /* == HELPERS == */
    function foGetInitials(name) { return (name || 'FO').split(' ').map(function (w) { return w[0]; }).join('').substring(0, 2).toUpperCase(); }
    function foGetPill(active) {
        return active
            ? '<span class="status-pill pill-active"><span class="pill-dot"></span>Active</span>'
            : '<span class="status-pill pill-inactive"><span class="pill-dot"></span>Inactive</span>';
    }
    function foFormatDate(d) {
        if (!d) return '—';
        var dt = new Date(d);
        return isNaN(dt) ? String(d) : dt.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
    }
    function foEsc(s) { return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
    function foShowToast(msg, err) {
        var el = document.getElementById('fo-toast');
        if (!el) return;
        el.innerHTML = msg;
        el.className = 'fo-toast' + (err ? ' error' : '') + ' show';
        setTimeout(function () { el.classList.remove('show'); }, 3500);
    }

})();
