/**
 * JWT Token Management Service
 * Handles token storage, retrieval, and automatic injection into API calls
 */

const AuthService = (function() {
    'use strict';

    const TOKEN_KEY = 'jwtToken';
    const USER_EMAIL_KEY = 'userEmail';
    const USER_ROLES_KEY = 'userRoles';
    const TENANT_ID_KEY = 'tenantId';
    const USER_ID_KEY = 'userId';

    /**
     * Store authentication data in localStorage
     */
    function setAuthData(authData) {
        if (authData.accessToken) {
            localStorage.setItem(TOKEN_KEY, authData.accessToken);
        }
        if (authData.email) {
            localStorage.setItem(USER_EMAIL_KEY, authData.email);
        }
        if (authData.roles) {
            localStorage.setItem(USER_ROLES_KEY, Array.isArray(authData.roles) ? authData.roles.join(',') : authData.roles);
        }
        if (authData.tenantId) {
            localStorage.setItem(TENANT_ID_KEY, authData.tenantId);
        }
        if (authData.userId) {
            localStorage.setItem(USER_ID_KEY, authData.userId.toString());
        }
        console.log('Auth data stored in localStorage');
    }

    /**
     * Get JWT token from localStorage
     */
    function getToken() {
        return localStorage.getItem(TOKEN_KEY);
    }

    /**
     * Get user email from localStorage
     */
    function getUserEmail() {
        return localStorage.getItem(USER_EMAIL_KEY);
    }

    /**
     * Get user roles from localStorage
     */
    function getUserRoles() {
        const roles = localStorage.getItem(USER_ROLES_KEY);
        return roles ? roles.split(',') : [];
    }

    /**
     * Get tenant ID from localStorage
     */
    function getTenantId() {
        return localStorage.getItem(TENANT_ID_KEY);
    }

    /**
     * Get user ID from localStorage
     */
    function getUserId() {
        const userId = localStorage.getItem(USER_ID_KEY);
        return userId ? parseInt(userId) : null;
    }

    /**
     * Check if user is authenticated
     */
    function isAuthenticated() {
        const token = getToken();
        if (!token) return false;

        // Check if token is expired
        try {
            const payload = parseJwt(token);
            const expiry = payload.exp * 1000; // Convert to milliseconds
            return Date.now() < expiry;
        } catch (e) {
            console.error('Error parsing token:', e);
            return false;
        }
    }

    /**
     * Parse JWT token to extract payload
     */
    function parseJwt(token) {
        try {
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(atob(base64).split('').map(function(c) {
                return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
            }).join(''));
            return JSON.parse(jsonPayload);
        } catch (e) {
            console.error('Error parsing JWT:', e);
            return null;
        }
    }

    /**
     * Clear all authentication data
     */
    function clearAuthData() {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_EMAIL_KEY);
        localStorage.removeItem(USER_ROLES_KEY);
        localStorage.removeItem(TENANT_ID_KEY);
        localStorage.removeItem(USER_ID_KEY);
        console.log('Auth data cleared from localStorage');
    }

    /**
     * Get authorization headers for API calls
     */
    function getAuthHeaders() {
        const headers = {
            'Content-Type': 'application/json'
        };

        const token = getToken();
        if (token) {
            headers['Authorization'] = 'Bearer ' + token;
        }

        const tenantId = getTenantId();
        if (tenantId) {
            headers['X-Tenant-ID'] = tenantId;
        }

        const roles = getUserRoles();
        if (roles.length > 0) {
            headers['X-User-Role'] = roles[0]; // Send primary role
        }

        return headers;
    }

    /**
     * Make authenticated fetch request
     */
    async function fetchWithAuth(url, options = {}) {
        // Ensure headers exist
        options.headers = options.headers || {};
        
        // Merge with auth headers
        const authHeaders = getAuthHeaders();
        Object.keys(authHeaders).forEach(key => {
            if (!options.headers[key]) {
                options.headers[key] = authHeaders[key];
            }
        });

        try {
            const response = await fetch(url, options);
            
            // Handle 401 Unauthorized
            if (response.status === 401) {
                console.warn('Unauthorized request, clearing auth data and redirecting to login');
                clearAuthData();
                window.location.href = '/Login';
                return null;
            }

            return response;
        } catch (error) {
            console.error('Fetch error:', error);
            throw error;
        }
    }

    /**
     * Make authenticated jQuery AJAX request
     */
    function ajaxWithAuth(options) {
        // Ensure headers exist
        options.headers = options.headers || {};
        
        // Merge with auth headers
        const authHeaders = getAuthHeaders();
        Object.keys(authHeaders).forEach(key => {
            if (!options.headers[key]) {
                options.headers[key] = authHeaders[key];
            }
        });

        // Add error handler for 401
        const originalError = options.error;
        options.error = function(xhr, status, error) {
            if (xhr.status === 401) {
                console.warn('Unauthorized request, clearing auth data and redirecting to login');
                clearAuthData();
                window.location.href = '/Login';
                return;
            }
            if (originalError) {
                originalError(xhr, status, error);
            }
        };

        return $.ajax(options);
    }

    /**
     * Check if user has specific role
     */
    function hasRole(role) {
        const roles = getUserRoles();
        return roles.includes(role);
    }

    /**
     * Check if user is admin
     */
    function isAdmin() {
        return hasRole('Admin') || hasRole('admin');
    }

    // Public API
    return {
        setAuthData: setAuthData,
        getToken: getToken,
        getUserEmail: getUserEmail,
        getUserRoles: getUserRoles,
        getTenantId: getTenantId,
        getUserId: getUserId,
        isAuthenticated: isAuthenticated,
        clearAuthData: clearAuthData,
        getAuthHeaders: getAuthHeaders,
        fetchWithAuth: fetchWithAuth,
        ajaxWithAuth: ajaxWithAuth,
        hasRole: hasRole,
        isAdmin: isAdmin,
        parseJwt: parseJwt
    };
})();

// Export for use in other scripts
if (typeof window !== 'undefined') {
    window.AuthService = AuthService;
}
