(function () {
    const host = document.querySelector(".update-tracker-page");
    if (!host) {
        return;
    }

    const trackerApiUrl = host.dataset.trackerApiUrl || "/api/updates/tracker";
    const trackerLoading = document.getElementById("trackerLoading");
    const trackerError = document.getElementById("trackerError");
    const trackerContent = document.getElementById("trackerContent");
    const timelineGrid = document.getElementById("timelineGrid");
    const channelGrid = document.getElementById("channelGrid");
    const releaseGrid = document.getElementById("releaseGrid");
    const dlcCard = document.getElementById("dlcCard");

    const formatDate = (value) => {
        if (!value) return "sem data";
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return "sem data";
        return date.toLocaleString("pt-BR", { dateStyle: "short", timeStyle: "short" });
    };

    const createList = (title, items) => {
        const normalized = Array.isArray(items) && items.length ? items : ["Sem itens nesta secao."];
        const listItems = normalized.map(item => "<li>" + item + "</li>").join("");
        return `
            <div class="tracker-list-block">
                <strong>${title}</strong>
                <ul>${listItems}</ul>
            </div>
        `;
    };

    const statusClass = (status) => {
        const normalized = (status || "").toLowerCase();
        if (normalized.includes("hotfix")) return "is-hotfix";
        if (normalized.includes("teste")) return "is-test";
        return "is-published";
    };

    const renderTimeline = (timeline) => {
        timelineGrid.innerHTML = "";
        channelGrid.innerHTML = "";

        (timeline || []).forEach(item => {
            const timelineCard = document.createElement("article");
            timelineCard.className = "timeline-card";
            timelineCard.innerHTML = `
                <div class="tracker-card-head">
                    <strong>${(item.channel || "CANAL").toUpperCase()}</strong>
                    <span class="status-pill ${statusClass(item.status)}">${item.status || "Sem status"}</span>
                </div>
                <p><code>${item.version || "n/a"}</code></p>
                <p>Atualizado em: ${formatDate(item.updatedAtUtc)}</p>
            `;
            timelineGrid.appendChild(timelineCard);

            const detailsCard = document.createElement("article");
            detailsCard.className = "timeline-card";
            detailsCard.innerHTML = `
                <div class="tracker-card-head">
                    <strong>${(item.channel || "CANAL").toUpperCase()} - ${item.installerAssetName || "Instalador"}</strong>
                    <span class="status-pill ${statusClass(item.status)}">${item.status || "Sem status"}</span>
                </div>
                <p><a href="${item.downloadUrl || "#"}" target="_blank" rel="noopener">Download direto</a></p>
                <p>SHA256: <code class="tracker-code">${item.installerSha256 || "n/a"}</code></p>
                ${createList("Changelog resumido", item.changelogSummary)}
                ${createList("Problemas conhecidos", item.knownIssues)}
                ${createList("Corrigido nesta versao", item.fixedInVersion)}
            `;
            channelGrid.appendChild(detailsCard);
        });
    };

    const renderDlc = (dlc) => {
        if (!dlc) return;
        dlcCard.innerHTML = `
            <div class="tracker-card-head">
                <strong>${dlc.label || "DDS-DLC"}</strong>
                <span class="status-pill is-published">${dlc.status || "Sem status"}</span>
            </div>
            <p>Stable: <code>${dlc.stableVersion || "n/a"}</code></p>
            <p>Beta: <code>${dlc.betaVersion || "n/a"}</code></p>
            <p>Atualizado em: ${formatDate(dlc.lastUpdatedAtUtc)}</p>
            <p>${dlc.publicNotes || ""}</p>
        `;
    };

    const renderReleases = (releases) => {
        releaseGrid.innerHTML = "";
        (releases || []).forEach(release => {
            const card = document.createElement("article");
            card.className = "timeline-card";
            card.innerHTML = `
                <div class="tracker-card-head">
                    <strong>${release.name || release.tagName || "Release"}</strong>
                    <span class="status-pill ${statusClass(release.status)}">${release.status || "Sem status"}</span>
                </div>
                <p>Tag: <code>${release.tagName || "n/a"}</code></p>
                <p>Publicado em: ${formatDate(release.publishedAtUtc)}</p>
                <p><a href="${release.htmlUrl || "#"}" target="_blank" rel="noopener">Abrir release</a></p>
                ${createList("Changelog resumido", release.changelogSummary)}
            `;
            releaseGrid.appendChild(card);
        });
    };

    fetch(trackerApiUrl)
        .then(response => {
            if (!response.ok) {
                throw new Error("HTTP " + response.status);
            }
            return response.json();
        })
        .then(data => {
            renderTimeline(data.timeline || []);
            renderDlc(data.dlcSummary || null);
            renderReleases(data.releases || []);
            trackerLoading.hidden = true;
            trackerError.hidden = true;
            trackerContent.hidden = false;
        })
        .catch(() => {
            trackerLoading.hidden = true;
            trackerContent.hidden = true;
            trackerError.hidden = false;
        });
})();
