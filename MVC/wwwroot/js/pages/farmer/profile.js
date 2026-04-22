$(document).ready(function () {
});

function saveProfile() {
    var acctNo = $("#acctNo").val();
    var ifsc   = $("#ifscCode").val();

    if (!acctNo || !ifsc) {
        fbAlert("Bank routing information cannot be left blank for active platform users.", "Missing Fields");
        return;
    }

    fbConfirm("Update profile?", "Are you sure you want to update your registered farm and bank routing information?", "Yes, Update").then(function (confirmed) {
        if (!confirmed) return;
        fbSuccess("Profile Updated", "Your Profile settings have been securely saved. New payouts will reflect these routing paths immediately.");
    });
}

function changeCover() {
    fbAlert("Profile cover image upload module initializing...", "Coming Soon");
}

function logoutUser() {
    fbConfirm("Logout", "Are you sure you want to securely log out of your session?", "Yes, Logout").then(function (confirmed) {
        if (!confirmed) return;
        window.location.href = "/Home/Login";
    });
}
