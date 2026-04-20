$(document).ready(function () {
});

function saveProfile() {
    var acctNo = $("#acctNo").val();
    var ifsc = $("#ifscCode").val();

    if (!acctNo || !ifsc) {
        kendo.alert("Bank routing information cannot be left blank for active platform users.");
        return;
    }

    kendo.confirm("Are you sure you want to update your registered farm and bank routing information?").then(function () {
        kendo.alert("We have securely updated your Profile settings. Your new payouts will reflect these routing paths immediately.");
    });
}

function changeCover() {
    kendo.alert("Profile cover image upload module initializing...");
}

function logoutUser() {
    kendo.confirm("Are you sure you want to securely log out of your session?").then(function () {
        window.location.href = "/Home/Login";
    });
}
