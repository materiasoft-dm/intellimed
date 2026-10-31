/** The start-up splash (same as index.html's), shown while the first route's code loads. */
export function Splash() {
  return (
    <div className="imed-splash">
      <div className="imed-splash-mark">
        <span className="imed-splash-ring" />
        <span className="imed-splash-logo">IM</span>
      </div>
      <h1 className="imed-splash-title">IntelliMed</h1>
      <p className="imed-splash-status">Starting up<span className="imed-splash-dots"><span>.</span><span>.</span><span>.</span></span></p>
    </div>
  );
}
