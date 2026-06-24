# copilot-runtime (Node 20 / Express + @copilotkit/runtime)
#
# Runs TypeScript directly via `tsx` (a devDependency), so the runtime image
# needs the full dependency tree — `npm ci --omit=dev` would strip `tsx` and
# break `npm run start`. Switching to a pruned production image requires either
# moving `tsx` to `dependencies` in package.json or adding a `tsc` build script
# (with a tsconfig outDir + module:NodeNext), both owned by the copilot-runtime
# agent, not this infra pass. Pinned to a specific minor for reproducibility.
FROM node:20.18-alpine

WORKDIR /app

# Create a non-root user and hand over /app BEFORE installing, so node_modules
# and application files are not owned by root. alpine adduser/addgroup use -S
# for system accounts (no home dir / login shell).
RUN addgroup -S app && adduser -S app -G app

COPY package.json package-lock.json ./
RUN npm ci --production=false

COPY . .
RUN chown -R app:app /app

USER app

EXPOSE 4000

CMD ["npm", "run", "start"]
