pipeline {
    agent any
    
    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        OUTPUT_DIR = 'output'
    }
    
    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        disableConcurrentBuilds()
        timeout(time: 120, unit: 'MINUTES')
    }
    
    parameters {
        string(name: 'TUTORIAL_URL', 
               defaultValue: 'https://abp.io/docs/latest/tutorials/book-store?UI=MVC&DB=EF', 
               description: 'Tutorial URL to validate')
        choice(name: 'PERSONA', 
               choices: ['junior', 'mid', 'senior'], 
               description: 'Developer persona level')
        choice(name: 'ENVIRONMENT', 
               choices: ['dev', 'staging', 'prod'], 
               description: 'Target environment')
        booleanParam(name: 'KEEP_CONTAINERS', 
                    defaultValue: false, 
                    description: 'Keep Docker containers after run')
    }
    
    stages {
        stage('Checkout & Setup') {
            steps {
                checkout scm
                sh '''
                    dotnet --version
                    dotnet tool install -g Volo.Abp.Studio.Cli
                    mkdir -p ${OUTPUT_DIR}
                '''
            }
        }
        
        stage('Restore Dependencies') {
            steps {
                sh 'dotnet restore'
            }
        }
        
        stage('Build Solution') {
            steps {
                sh 'dotnet build --configuration Release'
            }
        }
        
        stage('Run Tests') {
            steps {
                sh 'dotnet test --configuration Release --no-build --verbosity normal'
            }
        }
        
        stage('Validate Tutorial') {
            environment {
                AZURE_OPENAI_API_KEY = credentials('azure-openai-api-key')
                AZURE_OPENAI_ENDPOINT = credentials('azure-openai-endpoint')
                OPENAI_API_KEY = credentials('openai-api-key')
            }
            steps {
                script {
                    try {
                        sh """
                            dotnet run --project src/Validator.Orchestrator -- run \
                                --url "${params.TUTORIAL_URL}" \
                                --persona "${params.PERSONA}" \
                                --output ./${OUTPUT_DIR} \
                                ${params.KEEP_CONTAINERS ? '--keep-containers' : ''} \
                                --timeout ${params.ENVIRONMENT == 'prod' ? '120' : '60'}
                        """
                    } catch (Exception e) {
                        currentBuild.result = 'UNSTABLE'
                        echo "Validation completed with warnings: ${e.message}"
                    }
                }
            }
        }
        
        stage('Generate Reports') {
            steps {
                script {
                    if (fileExists("${env.OUTPUT_DIR}/summary.json")) {
                        def summary = readJSON file: "${env.OUTPUT_DIR}/summary.json"
                        
                        // Generate HTML report
                        sh """
                            dotnet run --project src/Validator.Orchestrator -- \
                                generate-report --input ${env.OUTPUT_DIR}/summary.json \
                                --output ${env.OUTPUT_DIR}/report.html
                        """
                        
                        // Archive results
                        archiveArtifacts artifacts: "${env.OUTPUT_DIR}/**/*", fingerprint: true
                        
                        // Publish HTML report
                        publishHTML([
                            allowMissing: false,
                            alwaysLinkToLastBuild: true,
                            keepAll: true,
                            reportDir: "${env.OUTPUT_DIR}",
                            reportFiles: 'report.html',
                            reportName: 'Tutorial Validation Report'
                        ])
                        
                        // Set build status based on validation result
                        if (summary.OverallStatus != 'Passed') {
                            currentBuild.result = 'UNSTABLE'
                        }
                    }
                }
            }
        }
        
        stage('Deploy') {
            when {
                anyOf {
                    branch 'main'
                    branch 'develop'
                }
            }
            steps {
                script {
                    if (params.ENVIRONMENT == 'prod' && currentBuild.result == 'SUCCESS') {
                        // Build and push Docker image
                        docker.withRegistry("${env.REGISTRY_URL}", "${env.REGISTRY_CREDENTIALS}") {
                            def customImage = docker.build("tutorial-validator:${env.BUILD_ID}", "-f ./docker/Dockerfile .")
                            customImage.push()
                        }
                    }
                }
            }
        }
    }
    
    post {
        always {
            // Clean up
            sh '''
                docker-compose -f ./docker/docker-compose.yml down -v --remove-orphans 2>/dev/null || true
                docker system prune -f 2>/dev/null || true
            '''
            
            // Send notifications
            script {
                if (currentBuild.result == 'SUCCESS') {
                    emailext (
                        subject: "Build Success: ${env.JOB_NAME} - #${env.BUILD_NUMBER}",
                        body: """
                            <p>Build successful for ${params.TUTORIAL_URL}</p>
                            <p>Persona: ${params.PERSONA}</p>
                            <p>Environment: ${params.ENVIRONMENT}</p>
                            <p>Build Number: #${env.BUILD_NUMBER}</p>
                            <p>View results: <a href="${env.BUILD_URL}">${env.JOB_NAME} - #${env.BUILD_NUMBER}</a></p>
                        """,
                        to: "${env.NOTIFICATION_EMAIL}"
                    )
                } else {
                    emailext (
                        subject: "Build Failed: ${env.JOB_NAME} - #${env.BUILD_NUMBER}",
                        body: """
                            <p>Build failed for ${params.TUTORIAL_URL}</p>
                            <p>Check console output: <a href="${env.BUILD_URL}console">${env.BUILD_URL}console</a></p>
                        """,
                        to: "${env.NOTIFICATION_EMAIL}"
                    )
                }
            }
        }
    }
}