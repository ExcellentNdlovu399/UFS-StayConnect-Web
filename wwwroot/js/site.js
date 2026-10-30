document.addEventListener("DOMContentLoaded", function () {

    const themeToggle =
        document.getElementById("themeToggle");

    const themeIcon =
        document.getElementById("themeIcon");


    const savedTheme =
        localStorage.getItem("theme");


    if (savedTheme === "dark") {

        document.documentElement
            .setAttribute("data-theme", "dark");

        themeIcon.className =
            "fa-solid fa-sun";

    }


    if (themeToggle) {

        themeToggle.addEventListener(
            "click",
            function () {

                const currentTheme =
                    document.documentElement
                        .getAttribute("data-theme");


                if (currentTheme === "dark") {

                    document.documentElement
                        .removeAttribute("data-theme");

                    localStorage.setItem(
                        "theme",
                        "light"
                    );

                    themeIcon.className =
                        "fa-solid fa-moon";

                }
                else {

                    document.documentElement
                        .setAttribute(
                            "data-theme",
                            "dark"
                        );

                    localStorage.setItem(
                        "theme",
                        "dark"
                    );

                    themeIcon.className =
                        "fa-solid fa-sun";

                }

            }
        );

    }

});

function scrollAccommodationCards(direction) {
    const slider = document.getElementById("accommodationSlider");

    if (!slider) {
        return;
    }

    const card = slider.querySelector(".accommodation-card");

    if (!card) {
        return;
    }

    const scrollAmount = card.offsetWidth + 24;

    slider.scrollBy({
        left: direction * scrollAmount,
        behavior: "smooth"
    });
}