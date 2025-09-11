document.querySelectorAll('.language-option').forEach(function (button) {
    button.addEventListener('click', function () {
        // Önceden seçili olan dilin "selected" sınıfını kaldır
        document.querySelectorAll('.language-option').forEach(function (btn) {
            btn.classList.remove('selected');
        });

        // Şu anda tıklanan butona "selected" sınıfı ekle
        button.classList.add('selected');
    });
});
