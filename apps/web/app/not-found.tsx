import Link from "next/link";

export default function NotFound() {
  return <main className="statePage"><span className="kicker">404 / SIGNAL LOST</span><h1>This opportunity surface no longer exists.</h1><p>The record may have expired, moved, or been removed at source.</p><Link className="primaryAction" href="/opportunities">Return to opportunity index</Link></main>;
}
