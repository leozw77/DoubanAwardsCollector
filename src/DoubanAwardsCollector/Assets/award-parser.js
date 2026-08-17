(() => {
  "use strict";

  const parserVersion = "1.0.2";
  const normalizeText = (value) =>
    (value || "").replace(/\s+/gu, " ").replace(/\u00a0/gu, " ").trim();

  const fail = (message) => ({
    ok: false,
    error: message,
    document: null,
  });

  const route = window.location.pathname.match(
    /^\/awards\/([^/]+)\/([^/]+)(?:\/.*)?$/u
  );

  if (!route) {
    return fail(`当前 URL 不是 Awards 届次页：${window.location.href}`);
  }

  const slug = decodeURIComponent(route[1]);
  const editionKey = decodeURIComponent(route[2]);

  const content = document.querySelector("#content");
  const article =
    document.querySelector("#content .article") ||
    content;

  const titleNode = document.querySelector("#content h1");

  if (!content || !article || !titleNode) {
    return fail(
      `未找到豆瓣 Awards 内容区域。title=${document.title}; url=${window.location.href}`
    );
  }

  const sourceTitle = normalizeText(titleNode.textContent);

  const cleanEventName = (title) => {
    let value = title;
    value = value.replace(/\(\s*(?:19|20)\d{2}\s*\)/gu, " ");
    value = value.replace(/第\s*[0-9一二三四五六七八九十百零〇]+\s*届/gu, " ");
    value = value.replace(/(?:的)?(?:获奖名单|提名名单|完整名单|名单)/gu, " ");
    value = value.replace(/\s*的\s*$/u, " ");
    value = normalizeText(value);
    return value || slug;
  };

  const eventName = cleanEventName(sourceTitle);

  const getYear = () => {
    const direct = sourceTitle.match(/\b((?:19|20)\d{2})\b/u);
    if (direct) {
      return Number(direct[1]);
    }

    for (const anchor of content.querySelectorAll("a[href]")) {
      let url;
      try {
        url = new URL(anchor.href, window.location.href);
      } catch {
        continue;
      }

      const match = url.pathname.match(
        /^\/awards\/([^/]+)\/([^/]+)\/?$/u
      );

      if (
        !match ||
        decodeURIComponent(match[1]) !== slug ||
        decodeURIComponent(match[2]) !== editionKey
      ) {
        continue;
      }

      const label = normalizeText(anchor.textContent);
      const year = label.match(/\b((?:19|20)\d{2})\b/u);
      if (year) {
        return Number(year[1]);
      }
    }

    return null;
  };

  const relatedEditions = [];
  const relatedSeen = new Set();

  // 豆瓣完整名单页底部的“历届…获奖名单”链接本身也是
  // /awards/{slug}/{edition}/nominees?k=a，而不是届次主页。
  // 只接受纯 4 位年份标签，避免把“回第XX届页面”等导航链接误收进来。
  for (const anchor of content.querySelectorAll('a[href*="/awards/"]')) {
    let url;
    try {
      url = new URL(anchor.href, window.location.href);
    } catch {
      continue;
    }

    const match = url.pathname.match(
      /^\/awards\/([^/]+)\/([^/]+)(?:\/nominees)?\/?$/u
    );

    if (!match || decodeURIComponent(match[1]) !== slug) {
      continue;
    }

    const label = normalizeText(anchor.textContent);
    if (!/^(?:19|20)\d{2}$/u.test(label)) {
      continue;
    }

    const relatedKey = decodeURIComponent(match[2]);
    if (relatedSeen.has(relatedKey)) {
      continue;
    }
    relatedSeen.add(relatedKey);

    const normalizedUrl = new URL(
      `/awards/${encodeURIComponent(slug)}/${encodeURIComponent(relatedKey)}/nominees`,
      window.location.origin
    );
    normalizedUrl.searchParams.set("k", "a");

    relatedEditions.push({
      editionKey: relatedKey,
      year: Number(label),
      label,
      url: normalizedUrl.href,
    });
  }

  const uniqueRefs = (refs) => {
    const byId = new Map();

    for (const ref of refs) {
      if (!ref.doubanId) {
        continue;
      }

      const existing = byId.get(ref.doubanId);
      if (!existing) {
        byId.set(ref.doubanId, ref);
        continue;
      }

      if (!existing.name && ref.name) {
        existing.name = ref.name;
      }
      if (!existing.url && ref.url) {
        existing.url = ref.url;
      }
    }

    return [...byId.values()];
  };

  const extractSubjects = (item) => {
    const refs = [];

    for (const anchor of item.querySelectorAll('a[href*="/subject/"]')) {
      let url;
      try {
        url = new URL(anchor.href, window.location.href);
      } catch {
        continue;
      }

      const match = url.pathname.match(/^\/subject\/([^/]+)\/?/u);
      if (!match) {
        continue;
      }

      refs.push({
        provider: "douban",
        doubanId: decodeURIComponent(match[1]),
        name: normalizeText(anchor.textContent || anchor.getAttribute("title")),
        url: url.href,
      });
    }

    return uniqueRefs(refs);
  };

  const extractPeople = (item) => {
    const refs = [];

    for (const anchor of item.querySelectorAll(
      'a[href*="/celebrity/"], a[href*="/personage/"]'
    )) {
      let url;
      try {
        url = new URL(anchor.href, window.location.href);
      } catch {
        continue;
      }

      const match = url.pathname.match(
        /^\/(?:celebrity|personage)\/([^/]+)\/?/u
      );
      if (!match) {
        continue;
      }

      refs.push({
        provider: "douban",
        doubanId: decodeURIComponent(match[1]),
        name: normalizeText(anchor.textContent || anchor.getAttribute("title")),
        url: url.href,
      });
    }

    return uniqueRefs(refs);
  };

  const categories = [];
  let currentGroup = "";
  let currentCategory = null;

  const nodes = article.querySelectorAll("h2, h3, h4, li");

  for (const node of nodes) {
    if (node.matches("h2") && !node.closest("li")) {
      currentCategory = null;
      continue;
    }

    if (node.matches("h3") && !node.closest("li")) {
      currentGroup = normalizeText(node.textContent);
      currentCategory = null;
      continue;
    }

    if (node.matches("h4") && !node.closest("li")) {
      const name = normalizeText(node.textContent);
      if (!name) {
        continue;
      }

      currentCategory = {
        order: categories.length,
        groupName: currentGroup,
        name,
        entries: [],
      };
      categories.push(currentCategory);
      continue;
    }

    if (!node.matches("li")) {
      continue;
    }

    if (!node.querySelector("a[href]")) {
      continue;
    }

    const rawText = normalizeText(node.textContent);
    if (!rawText) {
      continue;
    }

    const subjects = extractSubjects(node);
    const people = extractPeople(node);
    const imageNode = node.querySelector("img");

    if (subjects.length === 0 && people.length === 0 && !imageNode) {
      continue;
    }

    // Some Douban Awards groups are themselves the category:
    //
    //   h3  金狮奖 Golden Lion
    //   li  色，戒 ... 获奖
    //   li  ...
    //
    // There is no intermediate h4. In that layout, create an implicit
    // category only when we actually encounter a valid award entry.
    // This preserves the existing h3 -> h4 -> li behavior unchanged.
    if (!currentCategory) {
      if (!currentGroup) {
        continue;
      }

      currentCategory = {
        order: categories.length,
        groupName: currentGroup,
        name: currentGroup,
        entries: [],
      };
      categories.push(currentCategory);
    }

    let result = "unknown";
    if (/获奖/u.test(rawText)) {
      result = "winner";
    } else if (/提名/u.test(rawText)) {
      result = "nominee";
    } else {
      result = "nominee";
    }

    const imageOwner = (() => {
      if (!imageNode) {
        return { kind: "unknown", doubanId: "" };
      }

      const anchor = imageNode.closest("a[href]");
      if (!anchor) {
        return { kind: "unknown", doubanId: "" };
      }

      let url;
      try {
        url = new URL(anchor.href, window.location.href);
      } catch {
        return { kind: "unknown", doubanId: "" };
      }

      const subjectMatch = url.pathname.match(/^\/subject\/([^/]+)\/?/u);
      if (subjectMatch) {
        return {
          kind: "subject",
          doubanId: decodeURIComponent(subjectMatch[1]),
        };
      }

      const personMatch = url.pathname.match(
        /^\/(?:celebrity|personage)\/([^/]+)\/?/u
      );
      if (personMatch) {
        return {
          kind: "person",
          doubanId: decodeURIComponent(personMatch[1]),
        };
      }

      return { kind: "unknown", doubanId: "" };
    })();

    const image = imageNode
      ? {
          url:
            imageNode.currentSrc ||
            imageNode.getAttribute("src") ||
            imageNode.getAttribute("data-src") ||
            "",
          alt: normalizeText(
            imageNode.getAttribute("alt") ||
            imageNode.getAttribute("title")
          ),
          kind: imageOwner.kind,
          doubanId: imageOwner.doubanId,
        }
      : null;

    currentCategory.entries.push({
      order: currentCategory.entries.length,
      result,
      subjects,
      people,
      image,
      rawText,
    });
  }

  const nonEmptyCategories = categories.filter(
    (category) => category.entries.length > 0
  );

  if (nonEmptyCategories.length === 0) {
    return fail(
      `没有解析到奖项条目。可能是页面结构变化、验证页或并非完整名单。title=${sourceTitle}`
    );
  }

  return {
    ok: true,
    error: "",
    document: {
      schemaVersion: 1,
      parserVersion,
      collectedAtUtc: new Date().toISOString(),
      source: {
        provider: "douban",
        requestedUrl: "",
        finalUrl: window.location.href,
      },
      event: {
        slug,
        name: eventName,
        sourceTitle,
      },
      edition: {
        key: editionKey,
        year: getYear(),
        title: sourceTitle,
      },
      relatedEditions,
      categories: nonEmptyCategories,
    },
  };
})()
