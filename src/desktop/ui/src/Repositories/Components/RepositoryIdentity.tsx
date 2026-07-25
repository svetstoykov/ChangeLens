import type { RepositoryDescriptor } from "../Models/RepositoryDescriptor";
import { Icon } from "../../Visuals/Components/Icon";

interface RepositoryIdentityProps {
  readonly repository: RepositoryDescriptor;
}

export function RepositoryIdentity({ repository }: RepositoryIdentityProps) {
  const headName = getHeadName(repository);

  return (
    <section className="repository-identity" aria-label="Open repository">
      <div className="repository-identity-heading">
        <span className="repository-icon">
          <Icon name="folder" />
        </span>
        <div>
          <p>Open repository</p>
          <h2 title={repository.name}>{repository.name}</h2>
        </div>
      </div>
      <dl>
        <div className="repository-path">
          <dt>Path</dt>
          <dd title={repository.canonicalPath}>
            <code>{repository.canonicalPath}</code>
          </dd>
        </div>
        <div>
          <dt>Head</dt>
          <dd className="repository-head">
            <Icon
              name={repository.head.kind === "branch" ? "branch" : "detached"}
            />
            <code>{headName}</code>
          </dd>
        </div>
        <div>
          <dt>Revision</dt>
          <dd title={repository.head.revision}>
            <code>{repository.head.revision}</code>
          </dd>
        </div>
      </dl>
    </section>
  );
}

function getHeadName(repository: RepositoryDescriptor): string {
  switch (repository.head.kind) {
    case "branch":
      return repository.head.name;
    case "detached":
      return "Detached HEAD";
  }
}
