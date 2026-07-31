"use client";

export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return <main className="pageFrame"><section className="pageIntro"><span className="kicker">SERVICE INTERRUPTION</span><h1>The signal was<br/>interrupted.</h1><p className="lede">The underlying data remains safe. Retry the request or return to the opportunity index.</p><button className="headerAction" onClick={reset}>Retry request</button></section></main>;
}
