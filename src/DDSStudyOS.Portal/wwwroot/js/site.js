(() => {
  const expandableImages = Array.from(document.querySelectorAll('img[data-expandable="true"]'));
  if (!expandableImages.length) {
    return;
  }

  const backdrop = document.createElement("div");
  backdrop.className = "image-lightbox-backdrop";
  backdrop.innerHTML = `
    <div class="image-lightbox-card" role="dialog" aria-modal="true" aria-label="Visualizacao de imagem">
      <button type="button" class="image-lightbox-nav is-prev" aria-label="Imagem anterior">&#10094;</button>
      <button type="button" class="image-lightbox-nav is-next" aria-label="Proxima imagem">&#10095;</button>
      <button type="button" class="image-lightbox-close" aria-label="Fechar imagem">&times;</button>
      <img class="image-lightbox-image" alt="" />
      <div class="image-lightbox-caption"></div>
      <div class="image-lightbox-hint">Deslize no celular ou use as setas do teclado</div>
    </div>
  `;

  document.body.appendChild(backdrop);

  const card = backdrop.querySelector(".image-lightbox-card");
  const closeButton = backdrop.querySelector(".image-lightbox-close");
  const prevButton = backdrop.querySelector(".image-lightbox-nav.is-prev");
  const nextButton = backdrop.querySelector(".image-lightbox-nav.is-next");
  const lightboxImage = backdrop.querySelector(".image-lightbox-image");
  const caption = backdrop.querySelector(".image-lightbox-caption");
  const hint = backdrop.querySelector(".image-lightbox-hint");

  let isOpen = false;
  let currentIndex = -1;
  let touchStartX = 0;
  let touchStartY = 0;

  const closeLightbox = () => {
    if (!isOpen) {
      return;
    }

    backdrop.classList.remove("is-open");
    document.body.style.overflow = "";
    lightboxImage.removeAttribute("src");
    lightboxImage.removeAttribute("alt");
    caption.textContent = "";
    hint.textContent = "";
    currentIndex = -1;
    isOpen = false;
  };

  const renderCurrentImage = () => {
    const sourceImage = expandableImages[currentIndex];
    if (!sourceImage) {
      return;
    }

    const cardElement = sourceImage.closest("article");
    const title = cardElement?.querySelector("strong")?.textContent?.trim() ?? "";
    const description = cardElement?.querySelector("p, span")?.textContent?.trim() ?? "";

    lightboxImage.src = sourceImage.currentSrc || sourceImage.src;
    lightboxImage.alt = sourceImage.alt || title || "Imagem ampliada";
    caption.textContent = [title, description].filter(Boolean).join(" - ");
    hint.textContent = `Imagem ${currentIndex + 1} de ${expandableImages.length}`;
  };

  const openByIndex = (requestedIndex) => {
    if (!expandableImages.length) {
      return;
    }

    currentIndex = ((requestedIndex % expandableImages.length) + expandableImages.length) % expandableImages.length;
    renderCurrentImage();
    backdrop.classList.add("is-open");
    document.body.style.overflow = "hidden";
    isOpen = true;
  };

  const nextImage = () => {
    if (!isOpen) {
      return;
    }
    openByIndex(currentIndex + 1);
  };

  const previousImage = () => {
    if (!isOpen) {
      return;
    }
    openByIndex(currentIndex - 1);
  };

  expandableImages.forEach((image, index) => {
    image.addEventListener("click", () => openByIndex(index));
  });

  closeButton?.addEventListener("click", (event) => {
    event.stopPropagation();
    closeLightbox();
  });
  nextButton?.addEventListener("click", (event) => {
    event.stopPropagation();
    nextImage();
  });
  prevButton?.addEventListener("click", (event) => {
    event.stopPropagation();
    previousImage();
  });

  backdrop.addEventListener("click", (event) => {
    if (!card.contains(event.target)) {
      closeLightbox();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (!isOpen) {
      return;
    }

    if (event.key === "Escape") {
      closeLightbox();
    } else if (event.key === "ArrowRight") {
      nextImage();
    } else if (event.key === "ArrowLeft") {
      previousImage();
    }
  });

  lightboxImage.addEventListener("touchstart", (event) => {
    if (!isOpen || !event.touches.length) {
      return;
    }

    touchStartX = event.touches[0].clientX;
    touchStartY = event.touches[0].clientY;
  }, { passive: true });

  lightboxImage.addEventListener("touchend", (event) => {
    if (!isOpen || !event.changedTouches.length) {
      return;
    }

    const touchEndX = event.changedTouches[0].clientX;
    const touchEndY = event.changedTouches[0].clientY;
    const deltaX = touchEndX - touchStartX;
    const deltaY = touchEndY - touchStartY;

    if (Math.abs(deltaX) < 50 || Math.abs(deltaX) <= Math.abs(deltaY)) {
      return;
    }

    if (deltaX < 0) {
      nextImage();
    } else {
      previousImage();
    }
  }, { passive: true });
})();
