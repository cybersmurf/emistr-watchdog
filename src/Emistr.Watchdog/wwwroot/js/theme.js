/* Emistr Watchdog - Theme Switcher Logic */

// Theme management
const ThemeManager = {
    STORAGE_KEY: 'watchdog-theme',
    
    init() {
        // Load saved theme or detect system preference
        const savedTheme = localStorage.getItem(this.STORAGE_KEY);
        if (savedTheme) {
            this.setTheme(savedTheme);
        } else {
            this.setTheme('auto');
        }
        
        // Listen for system theme changes
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            if (this.getCurrentTheme() === 'auto') {
                this.applyTheme(e.matches ? 'dark' : 'light');
            }
        });
        
        // Update button states
        this.updateButtons();
    },
    
    getCurrentTheme() {
        return localStorage.getItem(this.STORAGE_KEY) || 'auto';
    },
    
    setTheme(theme) {
        localStorage.setItem(this.STORAGE_KEY, theme);
        
        if (theme === 'auto') {
            const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            this.applyTheme(prefersDark ? 'dark' : 'light');
        } else {
            this.applyTheme(theme);
        }
        
        this.updateButtons();
    },
    
    applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
    },
    
    updateButtons() {
        const current = this.getCurrentTheme();
        document.querySelectorAll('.theme-btn').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.theme === current);
        });
    },
    
    // Create theme switcher HTML
    createSwitcher() {
        return `
            <div class="theme-switcher">
                <button class="theme-btn" data-theme="light" onclick="ThemeManager.setTheme('light')" title="Light mode">☀️</button>
                <button class="theme-btn" data-theme="dark" onclick="ThemeManager.setTheme('dark')" title="Dark mode">🌙</button>
                <button class="theme-btn" data-theme="auto" onclick="ThemeManager.setTheme('auto')" title="Auto (system)">💻</button>
            </div>
        `;
    }
};

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', () => {
    ThemeManager.init();
});

