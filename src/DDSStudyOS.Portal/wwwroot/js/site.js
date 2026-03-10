(() => {
  const expandableImages = Array.from(document.querySelectorAll('img[data-expandable="true"]'));
  if (!expandableImages.length) {
    return;
  }

  const backdrop = document.createElement("div");
  backdrop.className = "image-lightbox-backdrop";
  backdrop.innerHTML = `
    <div class="image-lightbox-card" role="dialog" aria-modal="true" aria-label="Visualizacao de imagem">
      <button type="button" class="image-lightbox-close" aria-label="Fechar imagem">&times;</button>
      <img class="image-lightbox-image" alt="" />
      <div class="image-lightbox-caption"></div>
    </div>
  `;

  document.body.appendChild(backdrop);

  const card = backdrop.querySelector(".image-lightbox-card");
  const closeButton = backdrop.querySelector(".image-lightbox-close");
  const lightboxImage = backdrop.querySelector(".image-lightbox-image");
  const caption = backdrop.querySelector(".image-lightbox-caption");

  let isOpen = false;

  const closeLightbox = () => {
    if (!isOpen) {
      return;
    }

    backdrop.classList.remove("is-open");
    document.body.style.overflow = "";
    lightboxImage.removeAttribute("src");
    lightboxImage.removeAttribute("alt");
    caption.textContent = "";
    isOpen = false;
  };

  const openLightbox = (sourceImage) => {
    const cardElement = sourceImage.closest("article");
    const title = cardElement?.querySelector("strong")?.textContent?.trim() ?? "";
    const description = cardElement?.querySelector("p, span")?.textContent?.trim() ?? "";

    lightboxImage.src = sourceImage.src;
    lightboxImage.alt = sourceImage.alt || title || "Imagem ampliada";
    caption.textContent = [title, description].filter(Boolean).join(" - ");
    backdrop.classList.add("is-open");
    document.body.style.overflow = "hidden";
    isOpen = true;
  };

  expandableImages.forEach((image) => {
    image.addEventListener("click", () => openLightbox(image));
  });

  closeButton?.addEventListener("click", closeLightbox);

  backdrop.addEventListener("click", (event) => {
    if (!card.contains(event.target)) {
      closeLightbox();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      closeLightbox();
    }
  });
})();
