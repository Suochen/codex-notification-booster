const buttons = document.querySelectorAll('[data-lang]');
const docs = document.querySelectorAll('[data-doc]');

function setLanguage(lang) {
  document.documentElement.lang = lang === 'zh' ? 'zh-CN' : 'en';
  for (const doc of docs) {
    doc.hidden = doc.dataset.doc !== lang;
  }
  for (const button of buttons) {
    button.setAttribute('aria-pressed', String(button.dataset.lang === lang));
  }
  localStorage.setItem('cnb-language', lang);
}

const initial = localStorage.getItem('cnb-language') ||
  (navigator.language.toLowerCase().startsWith('zh') ? 'zh' : 'en');

for (const button of buttons) {
  button.addEventListener('click', () => setLanguage(button.dataset.lang));
}

setLanguage(initial);
