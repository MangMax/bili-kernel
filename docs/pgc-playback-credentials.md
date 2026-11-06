# PGC web playback credentials (#137 / application spec #131)

## Decision and first-party evidence

Inspected on 2026-11-06, without account credentials or account mutations:

* Bilibili's current player: https://s1.hdslb.com/bfs/static/player/main/core.js
  (SHA256 `BD01B8F87C303781772EA090774B475880BFD6F0B72D80D5E0CD1B845E796561`).
  Locate `HttpPlayUrl`, `getUrl`, and `getRemoteResponse`: PGC resolves to
  `api.bilibili.com/pgc/player/web/v2/playurl`; the GET passes `this.params`
  with `withCredentials:!0`. Searching this entire bundle found no `access_key`.
* Bilibili's older player: https://s1.hdslb.com/bfs/static/player/main/video.js
  (SHA256 `3CFD32684B8CFC8A8F67C3F7F27EF047A23796A2AF3BE8FD6CC3FDDDE8BFB195`).
  Locate `handlePlayurlRequest`: `seasonType>=1` selects `/pgc/player/web/playurl`;
  it constructs cid/qn/type/otype/fourk, avid or bvid, optional ep_id/fnver/fnval/session,
  and sends a GET with `withCredentials:!0`. No App access key is constructed.

These are executable first-party client contracts, not a published server API
guarantee. Re-fetch with `curl.exe -L -s <URL> -o <file>` and check SHA256; the URLs
are mutable. The web page itself returned an error page during research.

Both routes therefore explicitly use the existing `CookieOnly(needRID: true)`
policy: attach available cookies, never consult an unrelated App token. Keep WBI,
Referer/Origin, qn=64, fnval=4048, fourk, try_look and episode identity unchanged.
GET needs no CSRF. No evidence here supports a dual-credential requirement.

## Fallback and evidence limits

Authentication/permission and content rejections must propagate even if the legacy
fixture would succeed. Only an HTTP 404 route failure or an explicitly unsupported
successful playback representation may use the legacy route. API code -404 alone
does not establish that the route is absent; it can refer to unavailable content.
Malformed JSON, cancellation and transport failures are not compatibility signals.

The HTTP fixtures test real service/authenticator composition with synthetic
ordinary/entitled DASH tracks and format permissions. They prove preservation of
returned addresses, quality and permissions, and independently inspect primary
and fallback requests. They do **not** prove that a real ordinary or VIP account
receives those streams, that a Cookie is still valid, or that App and web account
entitlements are identical. Missing/expired Cookie cases simulate server rejection;
the client cannot infer expiry from the Cookie string. No live entitled-account
comparison or real-account mutation was performed. Existing format permission
mapping remains a quality-based client convention, not a server entitlement check.
