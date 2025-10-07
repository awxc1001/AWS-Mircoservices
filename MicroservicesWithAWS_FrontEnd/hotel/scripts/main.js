const currentUserToken = {
    currentUserId: "",
    idToken: "",
    sub: ""
};

// API endpoints used by the frontend
// Note: ListHotels expects the idToken as a query string parameter named "token"
const API_ENDPOINTS = {
    listHotels: "https://obqw3pnf2d.execute-api.ap-southeast-2.amazonaws.com/Test"
};

// ------- helpers -------
function getImageBaseUrl() {
    return (typeof config === 'object' && config.assets && typeof config.assets.imageBaseUrl === 'string')
        ? config.assets.imageBaseUrl.replace(/\/$/, '')
        : '';
}

function normalizeHotel(h) {
    return {
        name: h.name || h.Name || 'Unnamed',
        city: h.cityName || h.CityName || 'Unknown city',
        price: (h.price ?? h.Price) !== undefined ? (h.price ?? h.Price) : '-',
        rating: (h.rating ?? h.Rating) !== undefined ? (h.rating ?? h.Rating) : '-',
        fileName: h.fileName || h.FileName || '',
        imageUrl: h.ImageUrl || h.imageUrl || null
    };
}

function buildImageUrl(hotel) {
    if (hotel.imageUrl) return hotel.imageUrl;
    const base = getImageBaseUrl();
    if (!base || !hotel.fileName) return null;
    return `${base}/${encodeURIComponent(hotel.fileName)}`;
}

function pageLoad() {
    cognitoApp.auth.parseCognitoWebResponse(window.location.href);
    const currentUser = cognitoApp.auth.getCurrentUser();
    if (currentUser) {
        cognitoApp.auth.getSession();
        const sess = cognitoApp.auth.signInUserSession;
        currentUserToken.currentUserId = currentUser;
        if (sess?.idToken?.jwtToken) {
            currentUserToken.idToken = sess.idToken.jwtToken;
            try {
                const tokenDetails = parseJwt(sess.idToken.jwtToken);
                currentUserToken.sub = tokenDetails?.sub || "";
                const groups = tokenDetails?.['cognito:groups'];
                if (groups && groups[0] === 'Admin') {
                    const allowed = [
                        'http://localhost:8080/hotel/admin.html',
                        'http://localhost:8080/hotel/addHotel.html'
                    ];
                    if (!allowed.includes(window.location.href)) {
                        window.location.replace('http://localhost:8080/hotel/admin.html');
                    }
                }
            } catch (_) { /* ignore parse issues */ }
            // Print userId and idToken to console as requested
            try {
                console.log('userId:', currentUserToken.currentUserId);
                console.log('idToken:', currentUserToken.idToken);
            } catch (_) {}
        }
    }
    setButtonsVisibility(currentUser);
}

function parseJwt (token) {
    var base64Url = token.split('.')[1];
    var base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    var jsonPayload = decodeURIComponent(atob(base64).split('').map(function(c) {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
    }).join(''));

    return JSON.parse(jsonPayload);
}

function setAuthHeader()
{
    var form = $( "form" );
    form.submit(function( event ) {
        event.preventDefault(); // prevent the form from submitting normally
        
        // Validate authentication
        if (!currentUserToken.idToken) {
            alert("Please sign in first!");
            return;
        }
        
        // Create FormData for file upload
        var formData = new FormData(this);
        
        const xhr = new XMLHttpRequest();
        xhr.open('POST', form.attr('action'));
        xhr.setRequestHeader('Authorization', 'Bearer ' + currentUserToken.idToken);
        
        xhr.onreadystatechange = function() {
            if (xhr.readyState === XMLHttpRequest.DONE) {
                if (xhr.status === 200) {
                    alert("Hotel uploaded successfully!");
                    // Optionally redirect to admin page
                    // window.location.href = "admin.html";
                } else {
                    alert("Upload failed with status: " + xhr.status);
                }
            }
        };
        
        xhr.onerror = function() {
            alert("Network error occurred");
        };
        
        xhr.send(formData);
    });
}

function setButtonsVisibility(currentUser) {
    if (currentUser) {
        $("#btnSignIn").hide();
        $("#btnSignOut").show();
    } else {
        $("#btnSignIn").show();
        $("#btnSignOut").hide();
    }
}

// Fetch and render hotels for the current user
function loadHotels() {
    const $list = $('#hotelsList');
    if ($list.length === 0) return; // not on admin page
    if (!currentUserToken.idToken) {
        $list.empty().append('<li>Please sign in to view your hotels.</li>');
        return;
    }

    $list.empty().append('<li>Loading hotels…</li>');
    const url = `${API_ENDPOINTS.listHotels}?token=${encodeURIComponent(currentUserToken.idToken)}`;
    $.ajax({ url, method: 'GET', dataType: 'json' })
        .done(function (data) {
            renderHotels(Array.isArray(data) ? data : data);
        })
        .fail(function (xhr) {
            let msg = `Failed to load hotels (status ${xhr.status || 'n/a'})`;
            if (xhr.responseText) {
                try {
                    const err = JSON.parse(xhr.responseText);
                    if (err?.message) msg += `: ${err.message}`;
                } catch (_) {}
            }
            $list.empty().append(`<li>${msg}</li>`);
        });
}
function renderHotels(hotels) {
    const $list = $('#hotelsList');
    $list.empty();
    if (!hotels || hotels.length === 0) {
        $list.append('<li>No hotels found.</li>');
        return;
    }
    hotels.forEach(raw => {
        const h = normalizeHotel(raw);
        const img = buildImageUrl(h);
        const imgHtml = img ? `<img class="hotel-thumb" src="${img}" alt="${h.name}" onerror="this.style.display='none'" />` : '';
        const itemHtml = `
            <li class="hotel-item">
                ${imgHtml}
                <div class="hotel-info">
                    <strong>${h.name}</strong>
                    <div>City: ${h.city}</div>
                    <div>Price: ${h.price}</div>
                    <div>Rating: ${h.rating}</div>
                    ${img ? `<div><a href="${img}" target="_blank" rel="noopener">Open image</a></div>` : ''}
                </div>
            </li>`;
        $list.append(itemHtml);
    });
}

// Expose functions to global scope for inline handlers
if (typeof window !== 'undefined') {
    window.loadHotels = loadHotels;
    window.renderHotels = renderHotels;
}

// Add button event handlers
$(document).ready(function() {
    // Sign in button handler
    $("#btnSignIn").on("click", function () {
        var cognitoAuthSession = cognitoApp.auth.getSignInUserSession();
        if (cognitoAuthSession == null || !cognitoAuthSession.isValid()) {
            cognitoApp.auth.getSession();
        }
    });

    // Sign out button handler
    $("#btnSignOut").on("click", function () {
        cognitoApp.auth.signOut();
        window.location.replace("http://localhost:8080/hotel");
    });
});
