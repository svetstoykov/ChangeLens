import type { RepositoryDescriptor } from "../Models/RepositoryDescriptor";

interface RepositoryIdentityProps {
  readonly repository: RepositoryDescriptor;
  readonly repositoryGeneration?: number;
}

export function RepositoryIdentity({
  repository,
  repositoryGeneration,
}: RepositoryIdentityProps) {
  const headName = getHeadName(repository);

  return (
    <section
      aria-label="Open repository"
      data-repository-generation={repositoryGeneration}
    >
      <h2>{repository.name}</h2>
      <dl>
        <div>
          <dt>Path</dt>
          <dd>{repository.canonicalPath}</dd>
        </div>
        <div>
          <dt>Head</dt>
          <dd>{headName}</dd>
        </div>
        <div>
          <dt>Revision</dt>
          <dd>{repository.head.revision}</dd>
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
