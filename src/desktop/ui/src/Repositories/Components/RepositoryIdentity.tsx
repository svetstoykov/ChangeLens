import type { RepositoryDescriptor } from "../Models/RepositoryDescriptor";
import { LocalIcon } from "../../Visuals/Components/LocalIcon";
import branchIcon from "../../assets/branch.svg";
import detachedIcon from "../../assets/detached.svg";
import folderIcon from "../../assets/folder.svg";

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
      className="repository-identity"
      aria-label="Open repository"
      data-repository-generation={repositoryGeneration}
    >
      <div className="repository-identity-heading">
        <LocalIcon source={folderIcon} />
        <h2>{repository.name}</h2>
      </div>
      <dl>
        <div>
          <dt>Path</dt>
          <dd>
            <code>{repository.canonicalPath}</code>
          </dd>
        </div>
        <div>
          <dt>Head</dt>
          <dd className="repository-head">
            <LocalIcon
              source={
                repository.head.kind === "branch" ? branchIcon : detachedIcon
              }
            />
            <code>{headName}</code>
          </dd>
        </div>
        <div>
          <dt>Revision</dt>
          <dd>
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
