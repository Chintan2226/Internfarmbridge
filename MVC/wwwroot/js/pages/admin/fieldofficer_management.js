/**
 * Field Officer Management Logic
 * Handles Kendo Grid, Warehouse dropdown, and account creation/status toggles.
 */

(function ($) {
    'use strict';

    let foGrid;
    const API_BASE = window.API_BASE || 'http://localhost:5020/api/Admin';

    $(document).ready(function() {
        loadAndInitGrid();
        loadWarehouses();
    });

    async function loadAndInitGrid() {
        if (typeof window.showGridSkeleton === 'function') {
            window.showGridSkeleton('#foGrid', 5);
        }

        try {
            const res = await authFetch(API_BASE + "/GetFieldOfficers");
            const data = await res.json();
            
            $("#foGrid").empty();
            if (foGrid) {
                foGrid.destroy();
            }

            const items = data.data || data;

            foGrid = $("#foGrid").kendoGrid({
                dataSource: {
                    data: items,
                    schema: { 
                        model: {
                            id: "userId",
                            fields: {
                                fullName: { type: "string" },
                                email: { type: "string" },
                                warehouseName: { type: "string" },
                                totalInspections: { type: "number" },
                                isActive: { type: "boolean" }
                            }
                        }
                    },
                    pageSize: 10
                },
                columns: [
                    { 
                        field: "fullName", 
                        title: "OFFICER", 
                        width: 250,
                        template: function(d) {
                            const name = d.fullName || d.FullName || 'Unknown';
                            const email = d.email || d.Email || '';
                            return `<div class='d-flex align-items-center gap-3'>
                                        <div class='avatar-circle'>${name.charAt(0)}</div>
                                        <div><div class='fw-bold text-dark mb-0'>${name}</div><div class='x-small text-muted'>${email}</div></div>
                                    </div>`;
                        }
                    },
                    { 
                        field: "warehouseName", 
                        title: "WAREHOUSE", 
                        template: function(d) {
                            const w = d.warehouseName || d.WarehouseName || 'N/A';
                            return `<span class='badge bg-soft-success text-success border-0 px-3 py-2 rounded-pill font-md'>${w}</span>`;
                        }
                    },
                    { field: "assignedRegion", title: "REGION", width: 150 },
                    { 
                        field: "totalInspections", 
                        title: "PERFORMANCE", 
                        width: 150,
                        template: function(d) {
                            let count = d.totalInspections || d.TotalInspections || 0;
                            return `<div class='d-flex align-items-center gap-2 text-primary fw-bold'>
                                        <i class='fa fa-circle-check opacity-50'></i> ${count} Lots
                                    </div>`;
                        }
                    },
                    { 
                        field: "isActive", 
                        title: "STATUS", 
                        width: 120,
                        template: function(d) {
                            let active = d.isActive !== undefined ? d.isActive : d.IsActive;
                            return active 
                                ? `<span class="badge bg-success-vivid">Active</span>` 
                                : `<span class="badge bg-danger-vivid">Inactive</span>`;
                        }
                    },
                    { 
                        title: "CONTROL", 
                        width: 180,
                        template: function(d) {
                            let active = d.isActive !== undefined ? d.isActive : d.IsActive;
                            let userId = d.userId || d.UserId;
                            return `
                            <button class="btn btn-sm ${active ? 'btn-outline-danger' : 'btn-outline-success'} rounded-pill w-100 fw-bold border-2 px-3" 
                                onclick="toggleFOStatus(${userId}, ${active})">
                                ${active ? 'Deactivate' : 'Activate'}
                            </button>`;
                        }
                    }
                ],
                pageable: {
                    refresh: true,
                    buttonCount: 5
                },
                sortable: true,
                noRecords: { template: "<div class='p-5 text-center text-muted'>No field officers found.</div>" }
            }).data("kendoGrid");
        } catch (err) {
            $("#foGrid").html("<div class='p-5 text-center text-danger'>Connection error.</div>");
        }
    }

    async function loadWarehouses() {
        try {
            const res = await authFetch(API_BASE + "/GetWarehousesForDropdown");
            const json = await res.json();
            let html = '<option value="" disabled selected>Select Warehouse</option>';
            if (json && json.data) {
                json.data.forEach(w => {
                    html += `<option value="${w.id || w.Id}">${w.name || w.Name}</option>`;
                });
            }
            $("#fo_warehouse").html(html);
        } catch (e) {}
    }

    window.openFOModal = function() {
        const form = $("#foForm");
        if (form.length) form[0].reset();
        $("#foModal").modal('show');
    };

    window.saveFO = async function() {
        let payload = {
            FullName: $("#fo_name").val(),
            Email: $("#fo_email").val(),
            Phone: $("#fo_phone").val(),
            WarehouseId: parseInt($("#fo_warehouse").val()),
            AssignedRegion: $("#fo_region").val()
        };

        if(!payload.Email || !payload.FullName || !payload.WarehouseId) { 
            Swal.fire({ title: "Please fill all required fields.", icon: "warning" });
            return; 
        }

        try {
            const res = await authFetch(API_BASE + "/CreateFieldOfficer", {
                method: "POST",
                body: JSON.stringify(payload)
            });
            const data = await res.json();
            if (res.ok) {
                $("#foModal").modal('hide');
                loadAndInitGrid();
                Swal.fire({ title: data.message || "Created", icon: "success" });
            } else {
                Swal.fire({ title: data.message || "Error", icon: "error" });
            }
        } catch (err) {
            Swal.fire({ title: "Connection error", icon: "error" });
        }
    };

    window.toggleFOStatus = function(uid, currentStatus) {
        const action = currentStatus ? "DEACTIVATE" : "ACTIVATE";
        Swal.fire({
            title: `Change status to ${action}?`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#10b981',
            cancelButtonColor: '#d33',
            confirmButtonText: 'Yes'
        }).then(r => {
            if(r.isConfirmed) {
                executeToggle(uid, currentStatus);
            }
        });
    };

    async function executeToggle(uid, currentStatus) {
        try {
            const res = await authFetch(API_BASE + "/ToggleFOStatus", {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: `userId=${uid}&status=${!currentStatus}`
            });
            if (res.ok) {
                loadAndInitGrid();
            }
        } catch (e) {}
    }

})(jQuery);
