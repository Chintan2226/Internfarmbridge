/**
 * Vendor Profile Logic
 * Handles profile loading, business info updates, location management, and security.
 */

(function () {
    const API_URL = window.API_BASE_URL || 'http://localhost:5020/api/Vendor';
    let vendorId = null;
    let deliveryLocations = [];
    let editingLocationId = null;

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

    function escapeHtml(str) {
        if (!str) return '';
        return str.toString().replace(/[&<>"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));
    }

    function showNotification(type, title, message) {
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                icon: type,
                title: title,
                text: message,
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true,
                background: '#ffffff',
                color: '#1e293b'
            });
        } else {
            console.log(`${type}: ${title} - ${message}`);
        }
    }

    // --- PROFILE LOADING ---
    async function loadProfile() {
        try {
            const response = await authorizedFetch(`${API_URL}/profile`);
            if (!response) return;

            const result = await response.json();

            if (result.success && result.data) {
                const profile = result.data;
                const stats = result.stats;

                const bizName = profile.businessName || profile.BusinessName || profile.contactPerson || profile.ContactPerson || 'Vendor';
                const nameEl = document.getElementById('profileName');
                if (nameEl) nameEl.innerText = bizName;
                
                updateAvatar(profile.profileImageUrl || profile.ProfileImageUrl, bizName);

                const memberEl = document.getElementById('memberSince');
                if (memberEl) {
                    memberEl.innerText = profile.memberSince ? new Date(profile.memberSince).toLocaleDateString(window.CURRENT_LANG === 'gu' ? 'gu-IN' : (window.CURRENT_LANG === 'hi' ? 'hi-IN' : 'en-IN'), { year: 'numeric', month: 'long', day: 'numeric' }) : 'N/A';
                }

                const ordersEl = document.getElementById('totalOrders');
                if (ordersEl) ordersEl.innerText = stats?.totalOrders || stats?.TotalOrders || 0;
                
                const spentEl = document.getElementById('totalSpent');
                if (spentEl) {
                    spentEl.innerText = '₹' + (stats?.totalSpent || stats?.TotalSpent || 0).toLocaleString('en-IN');
                }

                const bizInp = document.getElementById('businessName');
                if (bizInp) bizInp.value = profile.businessName || profile.BusinessName || '';
                
                const gstinInp = document.getElementById('gstin');
                if (gstinInp) {
                    const gstinVal = (profile.gstin ?? profile.Gstin ?? '').toString().trim();
                    gstinInp.value = gstinVal.length > 0 ? gstinVal : 'NOT PROVIDED';
                }
                
                const contactInp = document.getElementById('contactPerson');
                if (contactInp) contactInp.value = profile.contactPerson || profile.ContactPerson || '';
                
                const phoneInp = document.getElementById('phone');
                if (phoneInp) phoneInp.value = profile.phone || profile.Phone || '';
                
                const emailInp = document.getElementById('email');
                if (emailInp) emailInp.value = profile.email || profile.Email || '';

                vendorId = profile.vendorId || profile.VendorId;
            }

            await loadDeliveryLocations();

        } catch (error) {
            console.error('Error loading profile:', error);
            showNotification('error', 'Critical Error', 'Failed to synchronize profile data.');
        }
    }

    // --- LOCATION MANAGEMENT ---
    async function loadDeliveryLocations() {
        try {
            const response = await authorizedFetch(`${API_URL}/addresses`);
            if (!response) return;

            const result = await response.json();

            if (result.success) {
                deliveryLocations = result.data || [];
                renderDeliveryLocations();
            } else {
                deliveryLocations = [];
                renderDeliveryLocations();
            }
        } catch (error) {
            console.error('Error loading locations:', error);
            deliveryLocations = [];
            renderDeliveryLocations();
        }
    }

    function renderDeliveryLocations() {
        const container = document.getElementById('deliveryLocationsList');
        if (!container) return;
        if (!deliveryLocations.length) {
            container.innerHTML = `
                <div class="location-item full-width" style="text-align:center; padding: 60px; border-style: dashed;">
                    <i class="fi fi-rr-map-marker-cross" style="font-size: 40px; color: #94a3b8; display:block; margin-bottom:20px"></i>
                    <p style="color: #64748b; font-weight: 700; font-size: 18px;" data-i18n="prof_no_loc">No delivery locations found.</p>
                    <p style="font-size:14px; color: #94a3b8; margin-top:8px" data-i18n="prof_add_loc_desc">Add a location to start ordering produce.</p>
                </div>`;
            return;
        }

        container.innerHTML = deliveryLocations.map(loc => {
            const id = loc.id || loc.Id;
            const fbT = window.fbT || (k => k);
            return `
            <div class="location-item ${loc.isDefault || loc.IsDefault ? 'is-default' : ''}">
                ${(loc.isDefault || loc.IsDefault) ? `<span class="location-tag">${fbT('prof_loc_default')}</span>` : ''}
                <div class="loc-address">
                    ${escapeHtml(loc.addressLine1 || loc.address || loc.Address || '')}
                </div>
                <div class="loc-meta-grid">
                    <div class="loc-meta-item">
                        <i class="fi fi-rr-city"></i> <strong>${fbT('prof_loc_city')}:</strong> ${escapeHtml(loc.city || loc.City || '')}
                    </div>
                    <div class="loc-meta-item">
                        <i class="fi fi-rr-map"></i> <strong>${fbT('prof_loc_state')}:</strong> ${escapeHtml(loc.state || loc.State || '')}
                    </div>
                    <div class="loc-meta-item">
                        <i class="fi fi-rr-mailbox"></i> <strong>${fbT('prof_loc_pin')}:</strong> ${escapeHtml(loc.zipCode || loc.pincode || loc.Pincode || '')}
                    </div>
                </div>
                <div class="loc-actions">
                    <button class="action-btn btn-edit" onclick="window.openEditLocationModal(${id})">
                        <i class="fi fi-rr-edit"></i> ${fbT('prof_loc_edit')}
                    </button>
                    <button class="action-btn btn-delete" onclick="window.deleteLocation(${id})">
                        <i class="fi fi-rr-trash"></i> ${fbT('prof_loc_delete')}
                    </button>
                </div>
            </div>
        `; }).join('');
    }

    window.openLocationModal = function () {
        editingLocationId = null;
        const titleEl = document.getElementById('locationModalTitle');
        if (titleEl) titleEl.setAttribute('data-i18n', 'prof_add_loc_title');
        
        if (window.fbApplyLang) window.fbApplyLang(window.CURRENT_LANG);
        else if (window.fbInit) window.fbInit();
        
        document.getElementById('modalAddress').value = '';
        document.getElementById('modalCity').value = '';
        document.getElementById('modalState').value = '';
        document.getElementById('modalPincode').value = '';
        document.getElementById('modalIsDefault').checked = false;
        document.getElementById('locationModal').style.display = 'flex';
    };

    window.openEditLocationModal = function (id) {
        const location = deliveryLocations.find(l => (l.id || l.Id) === id);
        if (!location) return;

        editingLocationId = id;
        const titleEl = document.getElementById('locationModalTitle');
        if (titleEl) titleEl.setAttribute('data-i18n', 'prof_edit_loc_title');
        
        if (window.fbApplyLang) window.fbApplyLang(window.CURRENT_LANG);
        else if (window.fbInit) window.fbInit();
        
        document.getElementById('modalAddress').value = location.addressLine1 || location.address || '';
        document.getElementById('modalCity').value = location.city || '';
        document.getElementById('modalState').value = location.state || '';
        document.getElementById('modalPincode').value = location.zipCode || location.pincode || '';
        document.getElementById('modalIsDefault').checked = location.isDefault || false;
        document.getElementById('locationModal').style.display = 'flex';
    };

    window.closeLocationModal = function () {
        document.getElementById('locationModal').style.display = 'none';
        editingLocationId = null;
    };

    window.saveLocation = async function () {
        const address = document.getElementById('modalAddress').value.trim();
        const city = document.getElementById('modalCity').value.trim();
        const state = document.getElementById('modalState').value.trim();
        const pincode = document.getElementById('modalPincode').value.trim();
        const isDefault = document.getElementById('modalIsDefault').checked;

        if (!address || !city || !state || !pincode) {
            showNotification('warning', 'Incomplete Form', 'Please provide all required address details.');
            return;
        }

        const locationData = {
            addressLine1: address,
            city: city,
            state: state,
            zipCode: pincode,
            isDefault: isDefault
        };

        try {
            let response;
            if (editingLocationId) {
                response = await authorizedFetch(`${API_URL}/addresses/update/${editingLocationId}`, {
                    method: 'PUT',
                    body: JSON.stringify(locationData)
                });
            } else {
                response = await authorizedFetch(`${API_URL}/addresses/save`, {
                    method: 'POST',
                    body: JSON.stringify(locationData)
                });
            }

            const result = await response.json();
            if (result.success) {
                showNotification('success', 'Success', result.message);
                window.closeLocationModal();
                await loadDeliveryLocations();
            } else {
                showNotification('error', 'Failed', result.message);
            }
        } catch (error) {
            showNotification('error', 'Error', 'Something went wrong while saving location.');
        }
    };

    window.deleteLocation = async function (id) {
        if (typeof Swal === 'undefined') return;
        const fbT = window.fbT || (k => k);
        Swal.fire({
            title: fbT('prof_delete_confirm_title'),
            text: fbT('prof_delete_confirm_msg'),
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#ef4444',
            cancelButtonColor: '#94a3b8',
            confirmButtonText: fbT('prof_loc_delete'),
            cancelButtonText: fbT('prof_cancel'),
            borderRadius: '16px',
            customClass: {
                popup: 'glass-morphism'
            }
        }).then(async (result) => {
            if (result.isConfirmed) {
                Swal.fire({
                    title: fbT('prof_delete_loading'),
                    allowOutsideClick: false,
                    didOpen: () => { Swal.showLoading(); }
                });
                
                try {
                    const response = await authorizedFetch(`${API_URL}/addresses/delete/${id}`, { method: 'DELETE' });
                    const res = await response.json();
                    if (res.success) {
                        showNotification('success', 'Deleted', res.message);
                        await loadDeliveryLocations();
                    } else {
                        showNotification('error', 'Delete Failed', res.message || 'Unable to delete address.');
                    }
                } catch (error) {
                    showNotification('error', 'Error', 'Failed to delete location.');
                }
            }
        });
    };

    // --- AVATAR & PHOTO ---
    function updateAvatar(imageUrl, name) {
        const avatarDiv = document.getElementById('avatarPreview');
        const globalAvatar = document.getElementById('fvAvatar');
        if (!avatarDiv) return;
        
        avatarDiv.classList.remove('skeleton');
        
        if (imageUrl && (imageUrl.startsWith('data:image') || imageUrl.startsWith('http'))) {
            const imgHtml = `<img src="${imageUrl}" alt="Profile" style="width:100%; height:100%; border-radius:50%; object-fit:cover;">`;
            avatarDiv.innerHTML = imgHtml;
            if (globalAvatar) globalAvatar.innerHTML = imgHtml;
        } else {
            const initial = (name || 'V')[0].toUpperCase();
            avatarDiv.innerHTML = initial;
            if (globalAvatar) {
                globalAvatar.innerText = initial;
                globalAvatar.style.display = 'flex';
                globalAvatar.style.alignItems = 'center';
                globalAvatar.style.justifyContent = 'center';
            }
        }
    }

    window.uploadPhoto = async function (input) {
        const file = input.files[0];
        if (!file) return;

        if (file.size > 2 * 1024 * 1024) {
            showNotification('warning', 'File Too Large', 'Maximum image size is 2MB.');
            return;
        }

        const reader = new FileReader();
        reader.onload = async function (e) {
            const base64 = e.target.result;
            try {
                const response = await authorizedFetch(`${API_URL}/profile/upload-photo`, {
                    method: 'POST',
                    body: JSON.stringify({ imageBase64: base64 })
                });
                const result = await response.json();
                if (result.success) {
                    showNotification('success', 'Profile Updated', 'Profile photo changed successfully.');
                    updateAvatar(base64, document.getElementById('profileName')?.innerText || 'Vendor');
                    setTimeout(loadProfile, 500);
                }
            } catch (error) {
                showNotification('error', 'Upload Failed', 'Unable to upload profile photo.');
            }
        };
        reader.readAsDataURL(file);
    };

    // --- PROFILE ACTIONS ---
    window.saveProfile = async function () {
        const profileData = {
            vendorId: vendorId,
            businessName: document.getElementById('businessName')?.value,
            contactPerson: document.getElementById('contactPerson')?.value,
            phone: document.getElementById('phone')?.value,
            gstin: document.getElementById('gstin')?.value.trim()
        };

        if (!profileData.businessName || !profileData.phone) {
            showNotification('warning', 'Validation Error', 'Business name and phone are mandatory.');
            return;
        }

        try {
            const response = await authorizedFetch(`${API_URL}/profile/update`, {
                method: 'PUT',
                body: JSON.stringify(profileData)
            });
            const result = await response.json();
            if (result.success) {
                showNotification('success', 'Profile Saved', result.message);
                const nameEl = document.getElementById('profileName');
                if (nameEl) nameEl.innerText = profileData.businessName || profileData.contactPerson || 'Vendor';
                loadProfile();
            } else {
                showNotification('error', 'Update Failed', result.message);
            }
        } catch (error) {
            showNotification('error', 'Error', 'Failed to synchronize profile updates.');
        }
    };

    window.checkPasswordStrength = function () {
        const password = document.getElementById('newPw')?.value;
        const bar = document.getElementById('strengthBar');
        const text = document.getElementById('strengthText');
        if (!bar || !text) return;

        if (!password) {
            bar.style.width = '0';
            text.innerText = '';
            return;
        }

        let strength = 0;
        if (password.length >= 8) strength++;
        if (password.match(/[a-z]/) && password.match(/[A-Z]/)) strength++;
        if (password.match(/\d/)) strength++;
        if (password.match(/[^a-zA-Z\d]/)) strength++;

        const scores = [
            { w: '25%', c: '#ef4444', t: 'Weak' },
            { w: '50%', c: '#f59e0b', t: 'Moderate' },
            { w: '75%', c: '#10b981', t: 'Good' },
            { w: '100%', c: '#059669', t: 'Excellent' }
        ];

        const score = scores[strength - 1] || scores[0];
        bar.style.width = score.w;
        bar.style.background = score.c;
        text.innerText = score.t;
        text.style.color = score.c;
    };

    window.changePassword = async function () {
        const current = document.getElementById('currentPw')?.value;
        const newPw = document.getElementById('newPw')?.value;
        const confirm = document.getElementById('confirmPw')?.value;

        if (!current || !newPw || !confirm) {
            showNotification('warning', 'Missing Fields', 'Please fill all password fields.');
            return;
        }

        if (newPw !== confirm) {
            showNotification('error', 'Mismatch', 'Passwords do not match.');
            return;
        }

        try {
            const response = await authorizedFetch(`${API_URL}/profile/change-password`, {
                method: 'POST',
                body: JSON.stringify({ currentPassword: current, newPassword: newPw })
            });
            const result = await response.json();
            if (result.success) {
                showNotification('success', 'Security Updated', 'Your password has been changed.');
                if (document.getElementById('currentPw')) document.getElementById('currentPw').value = '';
                if (document.getElementById('newPw')) document.getElementById('newPw').value = '';
                if (document.getElementById('confirmPw')) document.getElementById('confirmPw').value = '';
                window.checkPasswordStrength();
            } else {
                showNotification('error', 'Failed', result.message);
            }
        } catch (error) {
            showNotification('error', 'Error', 'Could not process password change.');
        }
    };

    // --- INIT ---
    document.addEventListener('DOMContentLoaded', () => {
        loadProfile();
        if (window.fbInit) window.fbInit();
    });

})();
