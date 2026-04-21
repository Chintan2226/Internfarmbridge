(() => {
  console.log("tyuiop")
  const FARMER_API_BASE = "http://localhost:5020/api/Admin";
  const ADMIN_ID = 1;

  let currentPage = 1;
  let searchTimeout = null;
  let pendingDeactivateId = null;
  let currentPaymentFarmerId = null;
  let pendingApprovalPaymentId = null;

  document.addEventListener("DOMContentLoaded", () => {
    loadStats();
    loadFarmers();
  });

  // ── STATS ──────────────────────────────────────────────────────────────
  async function loadStats() {
    try {
      const res = await fetch(`${FARMER_API_BASE}/dashboard`);
      if (res.ok) {
        const d = await res.json();
        document.getElementById("stat-total").textContent = d.registeredFarmers;
        document.getElementById("stat-week").textContent =
          `+${d.farmersThisWeek} this week`;
        document.getElementById("stat-active").textContent = d.activeAccounts;
        document.getElementById("stat-rate").textContent =
          `${d.activeRate}% active rate`;
        document.getElementById("stat-deact").textContent = d.deactivated;
        document.getElementById("stat-month").textContent =
          `${d.deactivatedThisMonth} this month`;
        document.getElementById("stat-pending").textContent =
          d.pendingVerification;
      }
    } catch (e) {
      console.error("Stats Error:", e);
    }
  }

  // ── FARMER GRID ────────────────────────────────────────────────────────
  async function loadFarmers() {
    const query = document.getElementById("search-input").value;
    const container = document.getElementById("f-grid");
    try {
      const res = await fetch(
        `${FARMER_API_BASE}/list?searchTerm=${encodeURIComponent(query)}&pageNumber=${currentPage}`,
      );
      if (res.ok) {
        const json = await res.json();
        renderGrid(json.data || []);
        document.getElementById("page-indicator").textContent =
          `Page ${currentPage}`;
      }
    } catch (e) {
      container.innerHTML =
        '<div style="grid-column:1/-1; text-align:center; color:red;">Connection Error. Ensure Backend is running on port 5020.</div>';
    }
  }

  function renderGrid(farmers) {
    const container = document.getElementById("f-grid");
    if (!farmers.length) {
      container.innerHTML =
        '<div style="grid-column:1/-1; text-align:center; padding: 40px; color:var(--text-muted);">No farmers found on this page.</div>';
      return;
    }
    container.innerHTML = farmers
      .map((f) => {
        const initials = (f.fullName || "F").substring(0, 2).toUpperCase();
        const isAct = f.isActive;
        const statusHtml = isAct
          ? `<span class="status-pill pill-active"><span class="pill-dot"></span>Active</span>`
          : `<span class="status-pill pill-inactive"><span class="pill-dot"></span>Inactive</span>`;
        return `
            <div class="f-card">
                <div class="f-card-header">
                    <div class="avatar-circle">${initials}</div>
                    <div style="flex:1; overflow:hidden;">
                        <div class="f-name">${f.fullName || "No Name"}</div>
                        <div class="f-id">#FB-${f.userId}</div>
                    </div>
                    <div>${statusHtml}</div>
                </div>
                <div class="f-body">
                    <div><div class="f-label">Phone</div><div class="f-val">${f.phone || "—"}</div></div>
                    <div><div class="f-label">Location</div><div class="f-val" title="${f.location}">${f.location || "—"}</div></div>
                    <div style="grid-column:1/-1;"><div class="f-label">Email</div><div class="f-val">${f.email || "—"}</div></div>
                </div>
                <div class="f-footer">
                    <div class="f-reg">Reg: ${new Date(f.registrationDate).toLocaleDateString("en-IN")}</div>
                    <div class="actions-row">
                        <button class="btn btn-settle" onclick="openPaymentModal(${f.userId})" title="Review pending settlements">
                            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
                            Settle
                        </button>
                        <button class="btn btn-view" onclick="openDetail(${f.userId})">View</button>
                        ${
                          isAct
                            ? `<button class="btn btn-deactivate" onclick="triggerDeactivate(${f.userId})">Deactivate</button>`
                            : `<button class="btn btn-activate" onclick="updateStatus(${f.userId}, true, 'Manual Activation')">Activate</button>`
                        }
                    </div>
                </div>
            </div>`;
      })
      .join("");
  }

  // ── PAYMENTS MODAL ─────────────────────────────────────────────────────
  async function openPaymentModal(farmerId) {
    currentPaymentFarmerId = farmerId;
    alert("kjhgfd")
    document.getElementById("paymentModalOverlay").classList.add("open");
    const tbody = document.getElementById("pending-payments-body");
    tbody.innerHTML =
      '<tr><td colspan="5" style="text-align:center; padding:20px;">Loading...</td></tr>';
    try {
      const res = await fetch(
        `${FARMER_API_BASE}/${farmerId}/pending-payments`,
      );
      if (res.ok) {
        const data = (await res.json()).data;
        if (!data || data.length === 0) {
          tbody.innerHTML =
            '<tr><td colspan="5" style="text-align:center; padding:20px; color:var(--text-muted);">No pending 70% settlements found for this farmer.</td></tr>';
          return;
        }
        tbody.innerHTML = data
          .map(
            (p) => `
                    <tr>
                        <td>
                            <div style="font-weight:700;">${p.cropName}</div>
                            <div style="font-size:10px; color:var(--text-muted);">ID: #${p.paymentId}</div>
                        </td>
                        <td>
                            <div style="font-size:11px; color:var(--text-muted);">Total: ₹${p.totalAmount.toLocaleString("en-IN")}</div>
                            <div style="font-size:11px; color:var(--green-main); font-weight:700;">Adv: ₹${p.advancePaid.toLocaleString("en-IN")}</div>
                        </td>
                        <td style="color:var(--amber); font-weight:800; font-size:15px;">₹${p.balancePending.toLocaleString("en-IN")}</td>
                        <td><span style="font-size:11px; color:var(--text-muted); font-style:italic;">Auto-generated on approval</span></td>
                        <td style="text-align:right;">
                            <button class="btn-primary" style="padding:6px 12px; font-size:11px;" onclick="approvePayment(${p.paymentId})">Approve</button>
                        </td>
                    </tr>`,
          )
          .join("");
      }
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="5" style="text-align:center; color:red;">Failed to load payments.</td></tr>';
    }
  }

  function approvePayment(paymentId) {
    pendingApprovalPaymentId = paymentId;
    document.getElementById("confirmPaymentModal").classList.add("open");
  }

  async function executePaymentApproval() {
    if (!pendingApprovalPaymentId) return;
    const btn = document.getElementById("confirmPayBtn");
    btn.textContent = "Processing...";
    btn.disabled = true;
    try {
      const res = await fetch(`${FARMER_API_BASE}/approve-payment`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          PaymentId: pendingApprovalPaymentId,
          AdminId: ADMIN_ID,
        }),
      });
      if (res.ok) {
        showToast("Payment Approved & Released Successfully!");
        closeModal("confirmPaymentModal");
        openPaymentModal(currentPaymentFarmerId);
      } else {
        showToast("Failed to approve payment", true);
      }
    } catch (e) {
      showToast("API connection error", true);
    } finally {
      btn.textContent = "Yes, Approve";
      btn.disabled = false;
      pendingApprovalPaymentId = null;
    }
  }

  // ── SEARCH & PAGINATION ────────────────────────────────────────────────
  function handleSearch() {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
      currentPage = 1;
      loadFarmers();
    }, 400);
  }
  function changePage(step) {
    if (currentPage + step > 0) {
      currentPage += step;
      loadFarmers();
    }
  }

  // ── ACTIVATE / DEACTIVATE ──────────────────────────────────────────────
  function triggerDeactivate(id) {
    pendingDeactivateId = id;
    document.getElementById("deactReason").value = "";
    document.getElementById("deactModalOverlay").classList.add("open");
  }
  function confirmDeactivate() {
    const reason = document.getElementById("deactReason").value.trim();
    if (!reason) return showToast("Please enter a reason", true);
    updateStatus(pendingDeactivateId, false, reason);
    closeModal("deactModalOverlay");
  }
  async function updateStatus(userId, status, reason) {
    const payload = {
      UserID: userId,
      Status: status,
      Reason: reason,
      AdminId: ADMIN_ID,
    };
    try {
      const res = await fetch(`${FARMER_API_BASE}/update-status`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });
      if (res.ok) {
        showToast(
          `Farmer ${status ? "activated" : "deactivated"} successfully`,
        );
        loadFarmers();
        loadStats();
      } else {
        showToast("Update failed", true);
      }
    } catch (e) {
      showToast("API connection error", true);
    }
  }

  // ── DETAIL MODAL ───────────────────────────────────────────────────────
  async function openDetail(id) {
    try {
      const res = await fetch(`${FARMER_API_BASE}/details/${id}`);
      if (!res.ok) throw new Error("Failed to fetch");
      const json = await res.json();
      const d = json.data;

      // Title
      document.getElementById("m-title").textContent =
        `Farmer Profile — ${d.profile.fullName}`;

      // ── TAB 1: PROFILE only ──────────────────────────────────────
      document.getElementById("profile-content").innerHTML = `
                <div class="info-box">
                    <div class="f-label">Full Name</div>
                    <div class="f-val">${d.profile.fullName || "—"}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">Farmer ID</div>
                    <div class="f-val">#FB-${d.profile.farmerID}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">Mobile</div>
                    <div class="f-val">${d.profile.mobileNumber || "—"}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">Email</div>
                    <div class="f-val">${d.profile.email || "—"}</div>
                </div>
                <div class="info-box" style="grid-column:1/-1;">
                    <div class="f-label">Location</div>
                    <div class="f-val">${d.profile.location || "—"}</div>
                </div>
            `;

      // ── TAB 2: BANK & KYC only ───────────────────────────────────
      const b = (d.bankDetails && d.bankDetails[0]) || {};
      document.getElementById("bank-content").innerHTML = `
                <div class="info-box">
                    <div class="f-label">Bank Name</div>
                    <div class="f-val">${b.bankName || "—"}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">Account No.</div>
                    <div class="f-val">${b.accountNumber || "—"}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">IFSC Code</div>
                    <div class="f-val">${b.ifscCode || "—"}</div>
                </div>
                <div class="info-box">
                    <div class="f-label">KYC Status</div>
                    <div class="f-val" style="color:var(--green-main); text-transform:capitalize;">${b.status || "Pending"}</div>
                </div>
            `;

      // ── TAB 3: CROP LISTINGS only ────────────────────────────────
      document.getElementById("crops-body").innerHTML =
        d.cropHistory && d.cropHistory.length
          ? d.cropHistory
              .map(
                (c) => `
                    <tr>
                        <td>${c.productName}</td>
                        <td>${c.quantity} kg</td>
                        <td>₹${c.price}</td>
                        <td><span style="background:var(--green-light); color:var(--green-main); padding:2px 8px; border-radius:10px; font-size:10px; font-weight:700;">${c.status}</span></td>
                        <td>${new Date(c.createdAt).toLocaleDateString("en-IN")}</td>
                    </tr>`,
              )
              .join("")
          : `<tr><td colspan="5" style="text-align:center; padding:20px; color:var(--text-muted);">No crops listed yet.</td></tr>`;

      // ── TAB 4: ORDERS only ───────────────────────────────────────
      document.getElementById("orders-body").innerHTML =
        d.orderHistory && d.orderHistory.length
          ? d.orderHistory
              .map(
                (o) => `
                    <tr>
                        <td style="font-weight:700;">#ORD-${o.orderID}</td>
                        <td>${o.cropName}</td>
                        <td>₹${o.amount}</td>
                        <td><span style="background:var(--blue-bg); color:var(--blue); padding:2px 8px; border-radius:10px; font-size:10px; font-weight:700;">${o.status}</span></td>
                        <td>${new Date(o.orderDate).toLocaleDateString("en-IN")}</td>
                    </tr>`,
              )
              .join("")
          : `<tr><td colspan="5" style="text-align:center; padding:20px; color:var(--text-muted);">No orders found.</td></tr>`;

      // Reset to Profile tab and open modal
      switchTab(
        "tab-profile",
        document.querySelector("#detailModalOverlay .tab-item"),
      );
      document.getElementById("detailModalOverlay").classList.add("open");
    } catch (e) {
      showToast("Failed to load farmer details", true);
    }
  }

  // ── MODAL UTILS ────────────────────────────────────────────────────────
  function closeModal(id) {
    document.getElementById(id).classList.remove("open");
  }

  function switchTab(tabId, el) {
    // Deactivate all tabs within the same modal
    const modal =
      el.closest(".modal-overlay") ||
      document.getElementById("detailModalOverlay");
    modal
      .querySelectorAll(".tab-item")
      .forEach((t) => t.classList.remove("active"));
    modal
      .querySelectorAll(".tab-pane")
      .forEach((p) => p.classList.remove("active"));
    el.classList.add("active");
    document.getElementById(tabId).classList.add("active");
  }

  function showToast(msg, isErr) {
    const t = document.getElementById("toast");
    t.textContent = msg;
    t.style.background = isErr ? "var(--red)" : "var(--green-dark)";
    t.classList.add("show");
    setTimeout(() => t.classList.remove("show"), 3000);
  }

  // Expose to global scope for inline onclick handlers
  window.loadFarmers = loadFarmers;
  window.handleSearch = handleSearch;
  window.changePage = changePage;
  window.openPaymentModal = openPaymentModal;
  window.approvePayment = approvePayment;
  window.executePaymentApproval = executePaymentApproval;
  window.triggerDeactivate = triggerDeactivate;
  window.confirmDeactivate = confirmDeactivate;
  window.updateStatus = updateStatus;
  window.openDetail = openDetail;
  window.closeModal = closeModal;
  window.switchTab = switchTab;
})();
